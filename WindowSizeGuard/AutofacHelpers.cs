#nullable enable

using System;
using System.Reflection;
using Autofac;
using JetBrains.Annotations;

namespace WindowSizeGuard {

    public static class AutofacHelpers {

        public static IContainer createContainer() {
            var containerBuilder = new ContainerBuilder();
            var assembly = Assembly.GetExecutingAssembly();

            containerBuilder.RegisterAssemblyTypes(assembly)
                            .Where(t => t.GetCustomAttribute<ComponentAttribute>() != null)
                            .AsImplementedInterfaces()
                            .AsSelf()
                            .InstancePerLifetimeScope()
                            .OnActivated(eventArgs => eventArgs.Instance.GetType().GetMethod("PostConstruct", new Type[0])?.Invoke(eventArgs.Instance, new object[0]));

            containerBuilder.RegisterAssemblyModules(assembly);

            return containerBuilder.Build();
        }

    }

    /// <summary>
    /// Automatically register this class in the Autofac container. It will use the <c>InstancePerLifetimeScope</c> scope.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    [MeansImplicitUse]
    public class ComponentAttribute: Attribute { }

}