#nullable enable

using ManagedWinapi.Windows;
using System;
using System.ComponentModel;
using System.Text.RegularExpressions;
using Unfucked;

namespace WindowSizeGuard;

public readonly struct WindowSelector {

    public readonly string? executableBaseNameWithoutExeExtension;
    public readonly string? className;
    public readonly Regex?  titlePattern;

    public WindowSelector(string? className = null, string? executableBaseName = null): this(executableBaseName, className, null, null) { }

    public WindowSelector(Regex title, string? className = null, string? executableBaseName = null): this(executableBaseName, className, null, title) { }

    public WindowSelector(string title, string? className = null, string? executableBaseName = null): this(executableBaseName, className, title, null) { }

    private WindowSelector(string? executableBaseName, string? className, string? title, Regex? titlePattern) {
        if (titlePattern != null && title != null) {
            throw new ArgumentException("Please specify at most 1 of the titlePattern and title arguments, not both.");
        } else if (executableBaseName == null && className == null && title == null && titlePattern == null) {
            throw new ArgumentException("Must specify at least 1 of the executableBaseName, className, title, and titlePattern arguments, not zero.");
        }

        this.className = className;

        executableBaseNameWithoutExeExtension = executableBaseName != null
            ? Regex.Replace(executableBaseName, @"\.exe$", string.Empty, RegexOptions.IgnoreCase)
            : null;

        if (titlePattern != null) {
            this.titlePattern = titlePattern;
        } else if (title != null) {
            this.titlePattern = new Regex("^" + Regex.Escape(title) + "$");
        } else {
            this.titlePattern = null;
        }
    }

    public bool matches(SystemWindow window) {
        try {
            return (className?.Equals(window.ClassName) ?? true) &&
                (titlePattern?.IsMatch(window.Title) ?? true) &&
                (executableBaseNameWithoutExeExtension?.Equals(window.GetProcessExecutableBasename()) ?? true);
        } catch (Win32Exception) {
            return false;
        } catch (InvalidOperationException) {
            return false;
        }
    }

}