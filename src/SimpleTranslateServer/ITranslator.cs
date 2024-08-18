namespace SimpleTranslateServer;

public interface ITranslator
{
    #region Public 方法

    Task<string> TranslateAsync(string content, string from, string to, CancellationToken cancellationToken);

    #endregion Public 方法
}
