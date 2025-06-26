using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LLMLib
{
    public class FoodRecommendationService
    {
        private readonly ChatClient _chatClient;
        private readonly ILogger<FoodRecommendationService> _logger;

        public FoodRecommendationService(string apiKey, ILogger<FoodRecommendationService> logger = null)
        {
            _chatClient = new ChatClient(model: "qwen-plus", credential: new ApiKeyCredential(apiKey), options: new OpenAIClientOptions
            {
                Endpoint = new Uri("https://dashscope.aliyuncs.com/compatible-mode/v1"),
            });
            _logger = logger;
        }

        /// <summary>
        /// 基于用户历史饮食数据推荐健康食物
        /// </summary>
        public async Task<string> RecommendHealthyFoodAsync(string userHistoryData)
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(ChatMessageContentPart.CreateTextPart(@"你是一位专业的营养师和美食推荐专家。请根据用户的历史饮食数据，推荐一种健康的食物。

要求：
1. 分析用户已经吃过的食物，尽量推荐用户没吃过但可能感兴趣的健康食物
2. 考虑营养均衡，补充用户饮食中可能缺失的营养成分
3. 食物要具有健康价值，如富含维生素、纤维、优质蛋白质等
4. 输出格式必须严格按照以下JSON格式：
{
  ""foodName"": ""食物名称"",
  ""reason"": ""推荐理由（20字以内，说明为什么健康和对人体的好处）""
}

注意：
- 只输出一个JSON对象，不要有其他内容
- 推荐理由要简洁明了，重点突出健康价值
- 食物名称要具体，如""蒸蛋羹""而不是""蛋类""")),
                new UserChatMessage(ChatMessageContentPart.CreateTextPart($"用户历史饮食数据：{userHistoryData}"))
            };

            try
            {
                ChatCompletion completion = await _chatClient.CompleteChatAsync(messages);
                var responseText = completion.Content[0].Text;
                
                _logger?.LogInformation("健康食物推荐响应: {Response}", responseText);
                
                // 提取JSON部分
                var jsonText = ExtractJsonFromResponse(responseText);
                
                // 尝试解析JSON响应
                try
                {
                    var recommendation = JsonSerializer.Deserialize<FoodRecommendation>(jsonText, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    if (recommendation != null && !string.IsNullOrEmpty(recommendation.FoodName))
                    {
                        _logger?.LogInformation("成功解析推荐结果: {FoodName} - {Reason}", recommendation.FoodName, recommendation.Reason);
                        return $"{recommendation.FoodName}|{recommendation.Reason}";
                    }
                }
                catch (JsonException ex)
                {
                    _logger?.LogWarning(ex, "解析推荐结果JSON失败: {JsonText}", jsonText);
                }
                
                // 如果JSON解析失败，尝试简单文本解析
                return ParseFallbackResponse(responseText);
                
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "健康食物推荐失败");
                throw new Exception($"获取健康推荐失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从响应中提取JSON部分
        /// </summary>
        private string ExtractJsonFromResponse(string response)
        {
            // 如果响应本身就是JSON格式
            if (response.TrimStart().StartsWith("{") && response.TrimEnd().EndsWith("}"))
            {
                return response.Trim();
            }

            // 使用正则表达式查找JSON对象
            var jsonPattern = @"\{[^{}]*""foodName""[^{}]*""reason""[^{}]*\}";
            var match = Regex.Match(response, jsonPattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Value;
            }

            // 查找包含```json的代码块
            var codeBlockPattern = @"```json\s*(\{[^}]*\})\s*```";
            var codeMatch = Regex.Match(response, codeBlockPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (codeMatch.Success)
            {
                return codeMatch.Groups[1].Value;
            }

            // 查找任何JSON对象
            var anyJsonPattern = @"\{[^{}]*\}";
            var anyMatch = Regex.Match(response, anyJsonPattern);
            if (anyMatch.Success)
            {
                return anyMatch.Value;
            }

            return response.Trim();
        }

        /// <summary>
        /// 备用解析方法
        /// </summary>
        private string ParseFallbackResponse(string response)
        {
            // 尝试从文本中提取食物名称和理由
            var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            string foodName = "健康蔬菜";
            string reason = "营养均衡，有益健康";

            foreach (var line in lines)
            {
                var cleanLine = line.Trim();
                if (cleanLine.Contains("食物") || cleanLine.Contains("推荐"))
                {
                    // 简单提取逻辑
                    var parts = cleanLine.Split('：', ':', ' ');
                    if (parts.Length > 1)
                    {
                        foodName = parts[1].Trim();
                        break;
                    }
                }
            }

            return $"{foodName}|{reason}";
        }

        /// <summary>
        /// 流式推荐健康食物
        /// </summary>
        public async Task RecommendHealthyFoodStreamAsync(string userHistoryData, Action<string> onContentReceived)
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(ChatMessageContentPart.CreateTextPart(@"你是一位专业的营养师和美食推荐专家。请根据用户的历史饮食数据，推荐一种健康的食物。

要求：
1. 分析用户已经吃过的食物，尽量推荐用户没吃过但可能感兴趣的健康食物
2. 考虑营养均衡，补充用户饮食中可能缺失的营养成分
3. 食物要具有健康价值，如富含维生素、纤维、优质蛋白质等
4. 输出格式必须严格按照以下JSON格式：
{
  ""foodName"": ""食物名称"",
  ""reason"": ""推荐理由（20字以内，说明为什么健康和对人体的好处）""
}

注意：
- 只输出一个JSON对象，不要有其他内容
- 推荐理由要简洁明了，重点突出健康价值
- 食物名称要具体，如""蒸蛋羹""而不是""蛋类""")),
                new UserChatMessage(ChatMessageContentPart.CreateTextPart($"用户历史饮食数据：{userHistoryData}"))
            };

            try
            {
                await foreach (StreamingChatCompletionUpdate update in _chatClient.CompleteChatStreamingAsync(messages))
                {
                    if (update.ContentUpdate != null && update.ContentUpdate.Count > 0)
                    {
                        foreach (var contentPart in update.ContentUpdate)
                        {
                            if (contentPart.Text != null)
                            {
                                onContentReceived?.Invoke(contentPart.Text);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "流式健康食物推荐失败");
                throw new Exception($"获取健康推荐失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 食物推荐结果的数据模型
        /// </summary>
        private class FoodRecommendation
        {
            public string FoodName { get; set; } = string.Empty;
            public string Reason { get; set; } = string.Empty;
        }
    }
}
