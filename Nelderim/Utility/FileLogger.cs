using System.Text;

namespace Nelderim.Utility;

public class FileLogger
{
    private readonly object fileLock = new();
    private readonly string datetimeFormat;
    private readonly string logFilename;
    private readonly string className;

    public FileLogger(Type type)
    {
        className = type.Name;
        datetimeFormat = "yyyy-MM-dd HH:mm:ss.fff";
        logFilename = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name + ".log";
    }

    public void Debug(string text)
    {
        WriteFormattedLog(LogLevel.DEBUG, text);
    }

    public void Error(string text)
    {
        WriteFormattedLog(LogLevel.ERROR, text);
    }

    public void Fatal(string text)
    {
        WriteFormattedLog(LogLevel.FATAL, text);
    }

    public void Info(string text)
    {
        WriteFormattedLog(LogLevel.INFO, text);
    }

    public void Trace(string text)
    {
        WriteFormattedLog(LogLevel.TRACE, text);
    }

    public void Warning(string text)
    {
        WriteFormattedLog(LogLevel.WARNING, text);
    }

    private void WriteLine(string text, bool append = false)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        lock (fileLock)
        {
            using (System.IO.StreamWriter writer = new System.IO.StreamWriter(logFilename, append, Encoding.UTF8))
            {
                writer.WriteLine(text);
            }
        }
    }

    private void WriteFormattedLog(LogLevel level, string text)
    {
        var sb = new StringBuilder();
        sb.Append(DateTime.Now.ToString(datetimeFormat));
        sb.Append(" ");
        sb.Append(className);
        sb.Append(" ");
        sb.Append(level);
        sb.Append(" ");

        WriteLine(sb + text, true);
    }

    [Flags]
    private enum LogLevel
    {
        TRACE,
        INFO,
        DEBUG,
        WARNING,
        ERROR,
        FATAL
    }
}