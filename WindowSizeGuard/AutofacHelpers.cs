#nullable enable

using Autofac;
using JetBrains.Annotations;
using System;
using System.Reflection;

namespace WindowSizeGuard;

public static class AutofacHelpers {

    public static IContainer createContainer() {
        ContainerBuilder containerBuilder = new();
        Assembly         assembly         = Assembly.GetExecutingAssembly();

        containerBuilder.RegisterAssemblyTypes(assembly)
            .Where(t => t.GetCustomAttribute<ComponentAttribute>() != null)
            .AsImplementedInterfaces()
            .AsSelf()
            .InstancePerLifetimeScope()
            .OnActivated(eventArgs => eventArgs.Instance.GetType().GetMethod("PostConstruct", Type.EmptyTypes)?.Invoke(eventArgs.Instance, Array.Empty<object>()));

        containerBuilder.RegisterAssemblyModules(assembly);

        return containerBuilder.Build();
    }

}

/// <summary>
/// Automatically register this class in the Autofac container. It will use the <c>InstancePerLifetimeScope</c> scope.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
[MeansImplicitUse]
public class ComponentAttribute: Attribute;