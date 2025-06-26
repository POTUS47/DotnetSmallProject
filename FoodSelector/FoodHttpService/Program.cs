using System;
using System.Net;
using System.IO;
using System.Collections.Generic;
using System.Text;
using FoodSelectorLib; // 引用 C++/CLI 的 DLL

class Program
{
    static void Main()
    {
        var listener = new HttpListener();
        listener.Prefixes.Add("http://localhost:5001/");
        listener.Start();
        Console.WriteLine("HTTP Server running on http://localhost:5001/");

        while (true)
        {
            var context = listener.GetContext();

            try
            {
                // 读取请求内容
                string requestBody;
                using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8)) // 明确指定编码
                {
                    requestBody = reader.ReadToEnd();
                }

                // 解析JSON请求体（假设客户端发送的是JSON数组）
                List<string> foods;
                try
                {
                    foods = System.Text.Json.JsonSerializer.Deserialize<List<string>>(requestBody);
                }
                catch
                {
                    // 如果解析失败，返回错误响应
                    SendErrorResponse(context, 400, "Invalid food list format. Please send a JSON array of strings.");
                    continue;
                }

                // 检查食物列表是否为空
                if (foods == null || foods.Count == 0)
                {
                    SendErrorResponse(context, 400, "Food list cannot be empty");
                    continue;
                }

                // 调用 C++/CLI 的 DLL 选择随机食物
                string selectedFood = FoodSelector.SelectRandomFood(foods);

                // 返回成功响应
                SendSuccessResponse(context, selectedFood);

                Console.WriteLine($"Request received, returned: {selectedFood}");
            }
            catch (Exception ex)
            {
                SendErrorResponse(context, 500, $"Internal server error: {ex.Message}");
                Console.WriteLine($"Error processing request: {ex}");
            }
        }
    }

    static void SendSuccessResponse(HttpListenerContext context, string message)
    {
        byte[] buffer = Encoding.UTF8.GetBytes(message);
        context.Response.StatusCode = 200;
        context.Response.ContentLength64 = buffer.Length;
        context.Response.OutputStream.Write(buffer, 0, buffer.Length);
        context.Response.Close();
    }

    static void SendErrorResponse(HttpListenerContext context, int statusCode, string message)
    {
        byte[] buffer = Encoding.UTF8.GetBytes(message);
        context.Response.StatusCode = statusCode;
        context.Response.ContentLength64 = buffer.Length;
        context.Response.OutputStream.Write(buffer, 0, buffer.Length);
        context.Response.Close();
        Console.WriteLine($"Error response sent: {statusCode} - {message}");
    }
}