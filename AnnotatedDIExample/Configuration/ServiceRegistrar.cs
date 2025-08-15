using AnnotatedDI.Attributes;
using AnnotatedDIExample.Attributes;
using AnnotatedDIExample.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace AnnotatedDI.Configuration;

public static class ServiceCollectionExtensions
{
    public static void RegisterAnnotatedServices(
        this IServiceCollection services,
        IConfiguration configuration,
        string? profilePropertyKey = null,
        ComponentScanOptions? componentScan = null)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        var profile = profilePropertyKey != null ? configuration[profilePropertyKey] : null;

        // 1) Descoberta de tipos candidatos (Service/Repository/Configuration)
        var allTypes = assemblies.SelectMany(a => SafeGetTypes(a)).ToList();

        var candidates = allTypes
            .Where(t =>
                ShouldScan(t, componentScan) &&
                (t.GetCustomAttribute<ServiceAttribute>() != null ||
                 t.GetCustomAttribute<RepositoryAttribute>() != null ||
                 t.GetCustomAttribute<ConfigurationAttribute>() != null))
            .Where(t => ShouldRegister(t, profile, configuration))
            .ToList();

        // 1.1) Descobrir métodos [Bean] válidos (respeitando filtros)
        var beanMethods = allTypes
            .Where(t => ShouldScan(t, componentScan) && t.GetCustomAttribute<ConfigurationAttribute>() != null)
            .Where(t => ShouldRegister(t, profile, configuration))
            .SelectMany(cfg =>
                cfg.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                   .Where(m => m.GetCustomAttribute<BeanAttribute>() != null)
                   .Where(m => ShouldRegister(m, profile, configuration))
                   .Select(m => new BeanMeta(cfg, m, m.ReturnType))
            )
            .ToList();

        // 2) Construir mapa "contrato → implementações" para Services/Repositories
        // Registra SOMENTE a primeira interface de cada classe (sua regra)
        var concreteServiceOrRepo = candidates
            .Where(t => t.GetCustomAttribute<ServiceAttribute>() != null || t.GetCustomAttribute<RepositoryAttribute>() != null)
            .ToList();

        var contractToImpl = new Dictionary<Type, List<Type>>();
        foreach (var impl in concreteServiceOrRepo)
        {
            var firstInterface = impl.GetInterfaces().FirstOrDefault();
            if (firstInterface != null)
            {
                if (!contractToImpl.TryGetValue(firstInterface, out var list))
                {
                    list = new List<Type>();
                    contractToImpl[firstInterface] = list;
                }
                list.Add(impl);
            }
        }

        // 3) Montar grafo de dependências (nós: concretes de service/repo, types de beans (tipo de retorno) e configurations)
        var graph = new Dictionary<Type, HashSet<Type>>(TypeComparer.Default);
        var allGraphNodes = new HashSet<Type>(TypeComparer.Default);

        // 3.1) Adiciona nós de serviços / repos
        foreach (var t in concreteServiceOrRepo)
        {
            allGraphNodes.Add(t);
            graph[t] = new HashSet<Type>(TypeComparer.Default);
        }

        // 3.2) Adiciona nós de configurations (não injetáveis)
        var configurations = candidates.Where(t => t.GetCustomAttribute<ConfigurationAttribute>() != null).ToList();
        foreach (var cfg in configurations)
        {
            allGraphNodes.Add(cfg);
            graph[cfg] = new HashSet<Type>(TypeComparer.Default);
        }

        // 3.3) Adiciona nós de beans (pelo tipo de retorno)
        var beanReturnTypes = new HashSet<Type>(beanMethods.Select(b => b.ReturnType), TypeComparer.Default);
        foreach (var r in beanReturnTypes)
        {
            allGraphNodes.Add(r);
            graph[r] = new HashSet<Type>(TypeComparer.Default);
        }

        // 3.4) Arestas: Service/Repo concretes → dependências (parametros do ctor)
        foreach (var t in concreteServiceOrRepo)
        {
            var ctor = PickGreediestCtor(t);
            if (ctor != null)
            {
                foreach (var dep in ctor.GetParameters())
                {
                    foreach (var depType in ExpandParamToDependencyTypes(dep.ParameterType, contractToImpl))
                    {
                        if (allGraphNodes.Contains(depType))
                            graph[t].Add(depType);
                        // se não está no grafo é externo (framework etc.), ignoramos para ordenação
                    }
                }
            }
        }

        // 3.5) Arestas: Configuration → dependências (parametros do ctor)
        foreach (var cfg in configurations)
        {
            var ctor = PickGreediestCtor(cfg);
            if (ctor != null)
            {
                foreach (var dep in ctor.GetParameters())
                {
                    foreach (var depType in ExpandParamToDependencyTypes(dep.ParameterType, contractToImpl))
                    {
                        if (allGraphNodes.Contains(depType))
                            graph[cfg].Add(depType);
                    }
                }
            }
        }

        // 3.6) Arestas: Bean(ReturnType) → Configuration dona e → dependências (parametros do método)
        foreach (var bm in beanMethods)
        {
            // depende da configuration dona
            graph[bm.ReturnType].Add(bm.ConfigurationType);
            // depende dos parâmetros do método
            foreach (var p in bm.Method.GetParameters())
            {
                foreach (var depType in ExpandParamToDependencyTypes(p.ParameterType, contractToImpl))
                {
                    if (allGraphNodes.Contains(depType))
                        graph[bm.ReturnType].Add(depType);
                }
            }
        }

        // 4) Ordenação topológica + detecção de ciclos
        var ordered = TopologicalSort(graph);

        // 5) Instanciação ordenada
        var baseProvider = services.BuildServiceProvider(); // para resolver externos ao seu mecanismo
        var createdConcrete = new Dictionary<Type, object>(TypeComparer.Default);                 // concrete class → instance
        var instancesByContract = new Dictionary<Type, List<object>>(TypeComparer.Default);       // contract/interface OR bean-return-type → instances
        var configInstances = new Dictionary<Type, object>(TypeComparer.Default);                 // configuration concrete → instance
        var beansByReturnType = new Dictionary<Type, List<object>>(TypeComparer.Default);         // return type (iface/class) → beans instances

        foreach (var node in ordered)
        {
            if (configurations.Contains(node))
            {
                // Configuration: criar, mas NÃO registrar no DI para injeção
                var cfgInstance = CreateByCtor(node, createdConcrete, instancesByContract, beansByReturnType, baseProvider);
                configInstances[node] = cfgInstance;
                continue;
            }

            if (concreteServiceOrRepo.Contains(node))
            {
                // Service/Repository concreto: criar e registrar
                var instance = CreateByCtor(node, createdConcrete, instancesByContract, beansByReturnType, baseProvider);

                // Regra: registra só a primeira interface, senão o próprio tipo
                var attr = node.GetCustomAttribute<ServiceAttribute>() as dynamic ?? node.GetCustomAttribute<RepositoryAttribute>() as dynamic;
                var lifetime = (attr != null && HasLifetime(attr)) ? GetLifetime(attr) : ServiceLifetime.Singleton;

                RegisterInterfaceOrSelfInstance(services, node, lifetime, instance);

                // Mapear concrete e sua primeira interface (se existir)
                createdConcrete[node] = instance;
                var firstInterface = node.GetInterfaces().FirstOrDefault();
                if (firstInterface != null)
                {
                    if (!instancesByContract.TryGetValue(firstInterface, out var list))
                    {
                        list = new List<object>();
                        instancesByContract[firstInterface] = list;
                    }
                    list.Add(instance);
                }
            }
            else if (beanReturnTypes.Contains(node))
            {
                // Beans: encontrar todos os métodos que retornam 'node', invocar e registrar
                var producers = beanMethods.Where(b => TypeComparer.Default.Equals(b.ReturnType, node)).ToList();
                foreach (var bm in producers)
                {
                    if (!configInstances.TryGetValue(bm.ConfigurationType, out var cfg))
                    {
                        // deve existir pela ordenação topológica
                        cfg = CreateByCtor(bm.ConfigurationType, createdConcrete, instancesByContract, beansByReturnType, baseProvider);
                        configInstances[bm.ConfigurationType] = cfg;
                    }

                    var methodArgs = bm.Method.GetParameters()
                        .Select(p => ResolveForParam(p.ParameterType, createdConcrete, instancesByContract, beansByReturnType, baseProvider))
                        .ToArray();

                    var bean = bm.Method.Invoke(cfg, methodArgs)
                               ?? throw new InvalidOperationException($"Bean method {bm.ConfigurationType.Name}.{bm.Method.Name} returned null.");

                    // Registrar como Singleton pelo tipo de retorno
                    services.AddSingleton(bm.ReturnType, bean);

                    // Armazenar para futuras injeções
                    if (!beansByReturnType.TryGetValue(bm.ReturnType, out var blist))
                    {
                        blist = new List<object>();
                        beansByReturnType[bm.ReturnType] = blist;
                    }
                    blist.Add(bean);

                    // Se o tipo de retorno é interface, ele também conta como "instância desse contrato"
                    if (bm.ReturnType.IsInterface)
                    {
                        if (!instancesByContract.TryGetValue(bm.ReturnType, out var ilist))
                        {
                            ilist = new List<object>();
                            instancesByContract[bm.ReturnType] = ilist;
                        }
                        ilist.Add(bean);
                    }
                    else
                    {
                        // se for classe concreta, também deixe acessível diretamente
                        createdConcrete[bm.ReturnType] = bean;
                    }
                }
            }
            else
            {
                // Nó externo (em tese não ocorre, mas caso ocorra, ignore)
            }
        }
    }

    // ========= Helpers principais =========

    private static IEnumerable<Type> ExpandParamToDependencyTypes(Type paramType, Dictionary<Type, List<Type>> contractToImpl)
    {
        // IEnumerable<T> → dependências das implementações de T (se houver)
        if (paramType.IsGenericType && paramType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            var t = paramType.GetGenericArguments()[0];
            if (contractToImpl.TryGetValue(t, out var impls))
                return impls; // depende de todas as implementações concretas
            return Enumerable.Empty<Type>();
        }

        // Param é interface? → se houver uma única implementação conhecida, depende dela; se várias, depende de TODAS (para ordenação),
        // mas a injeção em tempo de execução de "um único contrato" com várias impls dará erro (ambiguidade), o que é desejável para avisar o dev.
        if (paramType.IsInterface)
        {
            if (contractToImpl.TryGetValue(paramType, out var impls) && impls.Count > 0)
                return impls;
            return Enumerable.Empty<Type>();
        }

        // Param é classe concreta? → depende dela se ela for nó do grafo
        return new[] { paramType };
    }

    private static ConstructorInfo? PickGreediestCtor(Type t) =>
        t.GetConstructors()
         .OrderByDescending(c => c.GetParameters().Length)
         .FirstOrDefault();

    private static object CreateByCtor(
        Type type,
        IDictionary<Type, object> createdConcrete,
        IDictionary<Type, List<object>> instancesByContract,
        IDictionary<Type, List<object>> beansByReturnType,
        IServiceProvider baseProvider)
    {
        var ctor = PickGreediestCtor(type)
                   ?? throw new InvalidOperationException($"Type {type.FullName} does not have a public constructor.");

        var args = ctor.GetParameters()
            .Select(p => ResolveForParam(p.ParameterType, createdConcrete, instancesByContract, beansByReturnType, baseProvider))
            .ToArray();

        return Activator.CreateInstance(type, args)!;
    }

    private static object? ResolveForParam(
        Type requested,
        IDictionary<Type, object> createdConcrete,
        IDictionary<Type, List<object>> instancesByContract,
        IDictionary<Type, List<object>> beansByReturnType,
        IServiceProvider baseProvider)
    {
        // IEnumerable<T> → retorna uma lista com todas as instâncias registradas para T (serviços/repositórios/beans)
        if (requested.IsGenericType && requested.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            var t = requested.GetGenericArguments()[0];

            var list = new List<object>();

            if (instancesByContract.TryGetValue(t, out var svcList))
                list.AddRange(svcList);

            if (beansByReturnType.TryGetValue(t, out var beanList))
                list.AddRange(beanList);

            // também tenta pegar do provider base (caso externo)
            var ienumType = typeof(IEnumerable<>).MakeGenericType(t);
            var external = baseProvider.GetService(ienumType) as System.Collections.IEnumerable;
            if (external != null)
            {
                foreach (var item in external) list.Add(item!);
            }

            // construir List<T> concreta para passar como arg
            var listOfT = typeof(List<>).MakeGenericType(t);
            var concreteList = (System.Collections.IList)Activator.CreateInstance(listOfT)!;
            foreach (var item in list) concreteList.Add(item);

            return concreteList;
        }

        // Se pediram um tipo concreto que já criamos
        if (createdConcrete.TryGetValue(requested, out var concreteInstance))
            return concreteInstance;

        // Se pediram uma interface e já temos implementações criadas (serviços/repos/beans)
        if (requested.IsInterface)
        {
            var all = new List<object>();

            if (instancesByContract.TryGetValue(requested, out var svcList))
                all.AddRange(svcList);

            if (beansByReturnType.TryGetValue(requested, out var beanList))
                all.AddRange(beanList);

            if (all.Count == 1)
                return all[0];

            if (all.Count > 1)
                throw new InvalidOperationException(
                    $"Ambiguous dependency for contract '{requested.FullName}'. Found {all.Count} implementations. " +
                    $"Inject IEnumerable<{requested.Name}> or reduce to a single implementation.");

            // tenta provider base (externo ao seu mecanismo)
            var external = baseProvider.GetService(requested);
            if (external != null)
                return external;

            throw new InvalidOperationException(
                $"Unable to resolve dependency for '{requested.FullName}'. " +
                $"No implementations found or registered.");
        }

        // Tipo concreto ainda não criado: tenta provider base (externo)
        var ext = baseProvider.GetService(requested);
        if (ext != null)
            return ext;

        throw new InvalidOperationException($"Unable to resolve dependency '{requested.FullName}'.");
    }

    private static void RegisterInterfaceOrSelfInstance(
        IServiceCollection services,
        Type implType,
        ServiceLifetime lifetime,
        object instance)
    {
        var firstInterface = implType.GetInterfaces().FirstOrDefault();
        var serviceType = firstInterface ?? implType;
        services.Add(new ServiceDescriptor(serviceType, _ => instance, lifetime));
    }

    private static bool HasLifetime(object attr)
    {
        var prop = attr.GetType().GetProperty("Lifetime", BindingFlags.Public | BindingFlags.Instance);
        return prop != null;
    }

    private static ServiceLifetime GetLifetime(object attr)
    {
        var prop = attr.GetType().GetProperty("Lifetime", BindingFlags.Public | BindingFlags.Instance);
        if (prop != null && prop.GetValue(attr) is ServiceLifetime sl) return sl;
        return ServiceLifetime.Singleton;
    }

    // ========= Filtros/scan/condições =========

    private static bool ShouldRegister(MemberInfo typeOrMethod, string? profile, IConfiguration config)
    {
        // Profile
        var profileAttr = typeOrMethod.GetCustomAttribute<ProfileAttribute>();
        if (profileAttr != null)
        {
            var include = profileAttr.Include ?? Array.Empty<string>();
            var exclude = profileAttr.Exclude ?? Array.Empty<string>();

            if (!string.IsNullOrWhiteSpace(profile))
            {
                if (exclude.Any(x => EqualsIgnoreCaseTrim(x, profile))) return false;
                if (include.Length > 0 && !include.Any(x => EqualsIgnoreCaseTrim(x, profile))) return false;
            }
            else
            {
                // Sem profile atual definido: se há include explícito, não aplica
                if (include.Length > 0) return false;
            }
        }

        // ConditionalOnProperty
        var conditional = typeOrMethod.GetCustomAttribute<ConditionalOnPropertyAttribute>();
        if (conditional != null)
        {
            var value = config[conditional.Name];
            if (value == null) return conditional.MatchIfMissing;
            return value.Equals(conditional.HavingValue, StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }

    private static bool EqualsIgnoreCaseTrim(string a, string b) =>
        string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool ShouldScan(Type t, ComponentScanOptions? options)
    {
        if (options == null) return true;

        // por tipo explícito (ComponentScanAttribute em classe de app) — se você já usa isso, adapte aqui
        if (options.Include != null && options.Include.Length > 0)
        {
            // inclui se o namespace do tipo coincide com qualquer "Include"
            var includeNs = options.Include
                .Where(x => x != null)
                .Select(x => x.Namespace)
                .Where(ns => !string.IsNullOrWhiteSpace(ns))
                .Distinct()
                .ToList();

            if (includeNs.Count > 0 && !includeNs.Any(ns => IsSameOrSubNamespace(t.Namespace, ns)))
                return false;
        }

        if (options.Exclude != null && options.Exclude.Length > 0)
        {
            var excludeNs = options.Exclude
                .Where(x => x != null)
                .Select(x => x.Namespace)
                .Where(ns => !string.IsNullOrWhiteSpace(ns))
                .Distinct()
                .ToList();

            if (excludeNs.Any(ns => IsSameOrSubNamespace(t.Namespace, ns)))
                return false;
        }

        return true;
    }

    private static bool IsSameOrSubNamespace(string? candidate, string ns)
    {
        if (string.IsNullOrWhiteSpace(ns)) return true;
        if (string.IsNullOrWhiteSpace(candidate)) return false;
        if (candidate.Equals(ns, StringComparison.Ordinal)) return true;
        return candidate.StartsWith(ns + ".", StringComparison.Ordinal);
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly a)
    {
        try { return a.GetTypes(); }
        catch { return Array.Empty<Type>(); }
    }

    private static List<Type> TopologicalSort(Dictionary<Type, HashSet<Type>> graph)
    {
        var result = new List<Type>();
        var state = new Dictionary<Type, int>(TypeComparer.Default); // 0 = not visited, 1 = visiting, 2 = visited
        var stack = new Stack<Type>();

        foreach (var node in graph.Keys)
        {
            if (!state.ContainsKey(node))
                Visit(node, graph, state, result, stack);
        }

        return result;
    }

    private static void Visit(
        Type node,
        Dictionary<Type, HashSet<Type>> graph,
        Dictionary<Type, int> state,
        List<Type> result,
        Stack<Type> path)
    {
        if (state.TryGetValue(node, out var s))
        {
            if (s == 1)
            {
                // ciclo — monta caminho legível
                var cycle = path.Reverse().TakeWhile(t => !TypeComparer.Default.Equals(t, node)).Reverse().ToList();
                cycle.Add(node);
                var msg = "Cyclic dependency detected: " + string.Join(" → ", cycle.Select(t => t.FullName));
                throw new InvalidOperationException(msg);
            }
            if (s == 2) return;
        }

        state[node] = 1; // visiting
        path.Push(node);

        foreach (var dep in graph[node])
            Visit(dep, graph, state, result, path);

        path.Pop();
        state[node] = 2; // visited
        result.Add(node);
    }

    // Comparador para dicionários por Type como chave, garantindo consistência mesmo com tipos "iguais" entre contexts
    private sealed class TypeComparer : IEqualityComparer<Type>
    {
        public static readonly TypeComparer Default = new TypeComparer();
        public bool Equals(Type? x, Type? y) => x == y;
        public int GetHashCode(Type obj) => obj.GetHashCode();
    }
}

public class ComponentScanOptions
{
    /// <summary>
    /// Tipos marcadores para incluir namespaces (o namespace do tipo e subnamespaces).
    /// </summary>
    public Type[]? Include { get; set; }

    /// <summary>
    /// Tipos marcadores para excluir namespaces (o namespace do tipo e subnamespaces).
    /// </summary>
    public Type[]? Exclude { get; set; }
}

/// <summary>
/// Metadados de um método [Bean].
/// </summary>
internal sealed class BeanMeta
{
    public Type ConfigurationType { get; }
    public MethodInfo Method { get; }
    public Type ReturnType { get; }

    public BeanMeta(Type configurationType, MethodInfo method, Type returnType)
    {
        ConfigurationType = configurationType;
        Method = method;
        ReturnType = returnType;
    }
}