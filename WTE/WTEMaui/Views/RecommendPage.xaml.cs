using Microsoft.Maui.Controls;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using DataAccessLib.Services;

namespace WTEMaui.Views
{
    public partial class RecommendPage : ContentPage, INotifyPropertyChanged
    {
        private readonly HttpClient _httpClient = new();
        private const string BaseUrl = "http://localhost:5001"; // Android 模拟器用 10.0.2.2
        private readonly List<string> _defaultFoods = new() { "饺子", "拉面", "盖饭", "火锅", "寿司" };
        private readonly FoodService _foodService;

        // 绑定属性
        public string RecommendResult { get; set; } = string.Empty;
        public string StatusMsg { get; set; } = string.Empty;
        public bool HasResult { get; set; } = false;
        private readonly int _userId;

        public RecommendPage(FoodService foodService)
        {
            InitializeComponent();
            BindingContext = this;
            _foodService = foodService;
            _userId = App.CurrentUser?.UserId ?? 1;
        }

        private async void OnRandomClicked(object sender, EventArgs e)
        {
            StatusMsg = "正在获取您的历史食物...";
            OnPropertyChanged(nameof(StatusMsg));
            HasResult = false;
            OnPropertyChanged(nameof(HasResult));

            try
            {
                // 1. 获取用户历史食物
                var userHistoryFoods = await _foodService.GetUserHistoryFoodsAsync(_userId, 20);
                var foodNames = userHistoryFoods.Select(f => f.Name).ToList();

                // 如果用户没有历史食物，使用默认食物列表
                if (!foodNames.Any())
                {
                    foodNames = _defaultFoods;
                    StatusMsg = "使用默认食物列表进行推荐...";
                }
                else
                {
                    StatusMsg = $"基于您吃过的 {foodNames.Count} 种食物进行推荐...";
                }
                OnPropertyChanged(nameof(StatusMsg));

                // 2. 准备请求数据
                var json = JsonSerializer.Serialize(foodNames);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // 3. 发送 POST 请求
                var response = await _httpClient.PostAsync(BaseUrl, content);
                response.EnsureSuccessStatusCode();

                // 4. 更新界面
                RecommendResult = await response.Content.ReadAsStringAsync();
                HasResult = true;
                StatusMsg = string.Empty;
            }
            catch (HttpRequestException ex)
            {
                RecommendResult = string.Empty;
                StatusMsg = $"网络错误：{ex.Message}";
            }
            catch (Exception ex)
            {
                RecommendResult = string.Empty;
                StatusMsg = $"获取推荐失败：{ex.Message}";
            }

            OnPropertyChanged(nameof(RecommendResult));
            OnPropertyChanged(nameof(HasResult));
            OnPropertyChanged(nameof(StatusMsg));
        }

        private void OnHealthyClicked(object sender, EventArgs e)
        {
            // 健康推荐逻辑（可根据需求实现）
            StatusMsg = "健康推荐功能暂未实现";
            OnPropertyChanged(nameof(StatusMsg));
        }

        private async void OnSettingsClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync(nameof(SettingsPage));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}