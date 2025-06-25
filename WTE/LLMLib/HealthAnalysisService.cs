using DataAccessLib.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.Text.Json;

namespace LLMLib
{
    public class HealthAnalysisService
    {
        private readonly ChatClient _chatClient;
        private readonly ILogger<HealthAnalysisService> _logger;

        public HealthAnalysisService(string apiKey, ILogger<HealthAnalysisService> logger = null)
        {
            _chatClient = new ChatClient(model: "qwen-plus", credential: new ApiKeyCredential(apiKey), options: new OpenAIClientOptions
            {
                Endpoint = new Uri("https://dashscope.aliyuncs.com/compatible-mode/v1"),
            });
            _logger = logger;
        }

        public async Task<string> AnalyzeMealTimeAsync(string mealTimeData)
        {
            var messages = new List<ChatMessage>
                {
                    new SystemChatMessage(ChatMessageContentPart.CreateTextPart("你是一位专业的营养师，擅长分析和建议饮食计划。请根据以下数据，分析每餐的时间和类型，并给出合理的饮食建议。\r\n数据格式为JSON，包含每餐的类型、日期和时间。请注意，你只需要分析以下内容：\r\n分析每餐的时间间隔，判断是否合理，并给出用餐时间的调整建议。(**间隔一小时之内的认为是同一餐，不需要对一小时之内的多次进食做分析**)\r\n你不需要给出分析的过程，只需要给出最终的建议，不要绘制表格，总字数控制在500字以内。开头为\"根据历史用餐数据，以下是我的建议：\"，输出完列举的建议后停止输出。")),
                    new UserChatMessage(ChatMessageContentPart.CreateTextPart($"请分析以下数据并提供饮食建议：{mealTimeData}"))
                };

            ChatCompletion completion = await _chatClient.CompleteChatAsync(messages);

            return completion.Content[0].Text;
        }

        public async Task AnalyzeMealTimeStreamAsync(string mealTimeData, Action<string> onContentReceived)
        {
            var messages = new List<ChatMessage>
                {
                    new SystemChatMessage(ChatMessageContentPart.CreateTextPart("你是一位专业的营养师，擅长分析和建议饮食计划。请根据以下数据，分析每餐的时间和类型，并给出合理的饮食建议。\r\n数据格式为JSON，包含每餐的类型、日期和时间。请注意，你只需要分析以下内容：\r\n分析每餐的时间间隔，判断是否合理，并给出用餐时间的调整建议。(**间隔一小时之内的认为是同一餐，不需要对一小时之内的多次进食做分析**)\r\n你不需要给出分析的过程，只需要给出最终的建议，不要绘制表格，总字数控制在500字以内。开头为\"根据历史用餐数据，以下是我的建议：\"，输出完列举的建议后停止输出。")),
                    new UserChatMessage(ChatMessageContentPart.CreateTextPart($"请分析以下数据并提供饮食建议：{mealTimeData}"))
                };

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

        /// <summary>
        /// 分析用户详细饮食健康状况
        /// </summary>
        public async Task<string> AnalyzeDietHealthAsync(string detailedMealData)
        {
            var messages = new List<ChatMessage>
                {
                    new SystemChatMessage(ChatMessageContentPart.CreateTextPart("你是一位专业的营养师，擅长分析用户的饮食健康状况。请根据以下详细的饮食数据，分析用户的营养摄入情况并给出健康建议。\r\n数据格式为JSON，包含每餐的类型、日期、时间、具体食物名称和食物标签。请分析以下方面：\r\n1. 营养均衡性分析（蛋白质、碳水化合物、维生素、纤维等）\r\n2. 食物多样性评估\r\n3. 不健康食物摄入情况\r\n4. 针对性的饮食改善建议\r\n请给出实用的建议，总字数控制在600字以内。开头为\"根据您的详细饮食数据分析，以下是营养健康建议：\"，输出完建议后停止输出。")),
                    new UserChatMessage(ChatMessageContentPart.CreateTextPart($"请分析以下详细饮食数据并提供健康建议：{detailedMealData}"))
                };

            ChatCompletion completion = await _chatClient.CompleteChatAsync(messages);

            return completion.Content[0].Text;
        }

        /// <summary>
        /// 流式分析用户详细饮食健康状况
        /// </summary>
        public async Task AnalyzeDietHealthStreamAsync(string detailedMealData, Action<string> onContentReceived)
        {
            var messages = new List<ChatMessage>
                {
                    new SystemChatMessage(ChatMessageContentPart.CreateTextPart("你是一位专业的营养师，擅长分析用户的饮食健康状况。请根据以下详细的饮食数据，分析用户的营养摄入情况并给出健康建议。\r\n数据格式为JSON，包含每餐的类型、日期、时间、具体食物名称和食物标签。请分析以下方面：\r\n1. 营养均衡性分析（蛋白质、碳水化合物、维生素、纤维等）\r\n2. 食物多样性评估\r\n3. 不健康食物摄入情况\r\n4. 针对性的饮食改善建议\r\n请给出实用的建议，总字数控制在600字以内。开头为\"根据您的详细饮食数据分析，以下是营养健康建议：\"，输出完建议后停止输出。")),
                    new UserChatMessage(ChatMessageContentPart.CreateTextPart($"请分析以下详细饮食数据并提供健康建议：{detailedMealData}"))
                };

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
    }
}