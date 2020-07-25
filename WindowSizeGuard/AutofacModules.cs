#nullable enable

using Autofac;
using ManagedWinapi.Windows;

namespace WindowSizeGuard {

    public class ApplicationFrameWindowModule: Module {

        protected override void Load(ContainerBuilder builder) {
            builder.Register(context => SystemWindow.FilterToplevelWindows(window =>
                    window.ClassName == "ApplicationFrameWindow" && window.Process.ProcessName == "explorer")[0])
                .InstancePerLifetimeScope()
                .Named<SystemWindow>("ApplicationFrameWindow")
                .AsSelf();
        }

    }

}