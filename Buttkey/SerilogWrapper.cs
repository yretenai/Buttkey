using System.Diagnostics.CodeAnalysis;
using Buttbee;
using Serilog;

namespace Buttkey;

[SuppressMessage("ReSharper", "TemplateIsNotCompileTimeConstantProblem")]
internal class SerilogWrapper : IButtbeeLogger {
    internal SerilogWrapper(ILogger logger) => Logger = logger;

    private ILogger Logger { get; }

    public void Verbose(string message, params object?[] values) {
        Logger.Debug(message, values);
    }

    public void Info(string message, params object?[] values) {
        Logger.Debug(message, values);
    }

    public void Warn(string message, params object?[] values) {
        Logger.Debug(message, values);
    }

    public void Error(string message, params object?[] values) {
        Logger.Debug(message, values);
    }

    public void Critical(string message, params object?[] values) {
        Logger.Debug(message, values);
    }

    public void Critical(Exception e, string message, params object?[] values) {
        Logger.Debug(e, message, values);
    }

    public IButtbeeLogger AddContext(string key, string? value) => new SerilogWrapper(Logger.ForContext(key, value));

    public IButtbeeLogger AddContext<T>() =>
        // ReSharper disable once ContextualLoggerProblem
        new SerilogWrapper(Logger.ForContext<T>());
}
