using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Images;
using System;
using System.ClientModel;
using System.Data;
using System.IO;
using System.Threading.Tasks;

namespace LLMLib
{
    public class ImageRecognitionService
    {
        //private readonly OpenAIClient _openAIClient;
        private readonly ChatClient _chatClient;
        private readonly ILogger<ImageRecognitionService> _logger;

        public ImageRecognitionService(string apiKey, ILogger<ImageRecognitionService> logger = null)
        {
            //_openAIClient = new OpenAIClient(apiKey);
            _chatClient = new ChatClient(model: "qwen-vl-max-latest", credential: new ApiKeyCredential(apiKey), options: new OpenAIClientOptions
            {
                Endpoint = new Uri("https://dashscope.aliyuncs.com/compatible-mode/v1"),
            });
            _logger = logger;
        }

        public async Task<string> RecognizeFoodFromImageAsync(byte[] imageData, string imageFormat = "png")
        {
            try
            {
                _logger?.LogDebug("Starting image recognition...");

                // 创建二进制图片对象
                BinaryData imageBinary = BinaryData.FromBytes(imageData);

                // 构建消息
                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage(ChatMessageContentPart.CreateTextPart("You are a helpful assistant that identifies food items.")),
                    new UserChatMessage(ChatMessageContentPart.CreateTextPart("这是什么食物？请你直接给出他的名字和分类，不要有多余的解释。示例回答：【苹果/水果】或【西红柿炒鸡蛋/炒菜】"),
                    ChatMessageContentPart.CreateImagePart(imageBinary, $"image/{imageFormat}")),
                };

                // 创建聊天请求
                _logger?.LogDebug("Sending request to OpenAI...");
                ChatCompletion completion = await _chatClient.CompleteChatAsync(messages);

                _logger?.LogDebug("Received response: {Response}", completion);
                return completion.Content[0].Text;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Image recognition failed");
                throw new ApplicationException("食物识别失败，请重试", ex);
            }
        }

        public async Task<string> RecognizeFoodFromImageFileAsync(string filePath)
        {
            var imageData = await File.ReadAllBytesAsync(filePath);
            var fileExtension = Path.GetExtension(filePath).TrimStart('.');
            return await RecognizeFoodFromImageAsync(imageData, fileExtension);
        }
    }
}