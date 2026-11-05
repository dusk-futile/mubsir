using Sdk = Anthropic.Models.Messages;

namespace Mubsir.App.Brain;

internal static class ClaudeServerBlocks
{
    public static Sdk.ContentBlockParam? ToParam(Sdk.WebSearchToolResultBlock block)
    {
        if (block.Content.TryPickWebSearchResultBlocks(out var results))
        {
            var list = new List<Sdk.WebSearchResultBlockParam>(results.Count);
            foreach (var r in results)
            {
                list.Add(new Sdk.WebSearchResultBlockParam
                {
                    EncryptedContent = r.EncryptedContent,
                    Title = r.Title,
                    Url = r.Url,
                    PageAge = r.PageAge,
                });
            }

            return new Sdk.WebSearchToolResultBlockParam { ToolUseID = block.ToolUseID, Content = list };
        }

        if (block.Content.TryPickError(out var error))
        {
            return new Sdk.WebSearchToolResultBlockParam
            {
                ToolUseID = block.ToolUseID,
                Content = new Sdk.WebSearchToolRequestError { ErrorCode = error.ErrorCode },
            };
        }

        return null;
    }
}
