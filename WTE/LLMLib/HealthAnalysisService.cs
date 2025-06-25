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
    }
}