using Microsoft.Maui.Controls;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using DataAccessLib.Services;
using LLMLib;

namespace WTEMaui.Views
{
    public partial class RecommendPage : ContentPage, INotifyPropertyChanged
    {
        private readonly HttpClient _httpClient = new();
        private const string BaseUrl = "http://localhost:5001"; // Android 模拟器用 10.0.2.2
        private readonly List<string> _defaultFoods = new() { "饺子", "拉面", "盖饭", "火锅", "寿司" };
        private readonly FoodService _foodService;
        private readonly MealService _mealService;
        private readonly FoodRecommendationService _foodRecommendationService;
        private const int MinAnimationDuration = 3000; // 最少动画时间3秒

        // 绑定属性
        public string RecommendResult { get; set; } = string.Empty;
        public string RecommendFoodName { get; set; } = string.Empty;
        public string RecommendReason { get; set; } = string.Empty;
        public string StatusMsg { get; set; } = string.Empty;
        public bool HasResult { get; set; } = false;
        public bool IsAnimating { get; set; } = false;
        private readonly int _userId;

        public RecommendPage(FoodService foodService, MealService mealService, FoodRecommendationService foodRecommendationService)
        {
            InitializeComponent();
            BindingContext = this;
            _foodService = foodService;
            _mealService = mealService;
            _foodRecommendationService = foodRecommendationService;
            _userId = App.CurrentUser?.UserId ?? 1;
        }

        private async void OnRandomClicked(object sender, EventArgs e)
        {
            // 开始动画
            await StartLoadingAnimation();
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            string result = "";
            string status = "";
            bool success = false;
            
            // 重置所有结果
            RecommendResult = string.Empty;
            RecommendFoodName = string.Empty;
            RecommendReason = string.Empty;

            try
            {
                // 1. 获取用户历史食物
                var userHistoryFoods = await _foodService.GetUserHistoryFoodsAsync(_userId, 20);
                var foodNames = userHistoryFoods.Select(f => f.Name).ToList();

                // 如果用户没有历史食物，使用默认食物列表
                if (!foodNames.Any())
                {
                    foodNames = _defaultFoods;
                }

                // 2. 准备请求数据
                var json = JsonSerializer.Serialize(foodNames);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // 3. 发送 POST 请求
                var response = await _httpClient.PostAsync(BaseUrl, content);
                response.EnsureSuccessStatusCode();

                // 4. 获取结果
                result = await response.Content.ReadAsStringAsync();
                success = true;
            }
            catch (HttpRequestException ex)
            {
                status = $"网络错误：{ex.Message}";
            }
            catch (Exception ex)
            {
                status = $"获取推荐失败：{ex.Message}";
            }

            // 确保动画至少播放完整时间
            var elapsed = stopwatch.ElapsedMilliseconds;
            if (elapsed < MinAnimationDuration)
            {
                await Task.Delay(MinAnimationDuration - (int)elapsed);
            }

            // 停止加载动画
            await StopLoadingAnimation();

            // 更新结果
            if (success)
            {
                // 随机推荐只显示菜名，不显示理由
                RecommendFoodName = result.Trim();
                RecommendReason = "今日随机推荐，换个口味试试看！";
                StatusMsg = string.Empty;
                await ShowResultAnimation();
            }
            else
            {
                RecommendFoodName = string.Empty;
                RecommendReason = string.Empty;
                RecommendResult = string.Empty;
                StatusMsg = status;
                HasResult = false;
                OnPropertyChanged(nameof(HasResult));
            }

            OnPropertyChanged(nameof(RecommendFoodName));
            OnPropertyChanged(nameof(RecommendReason));
            OnPropertyChanged(nameof(RecommendResult));
            OnPropertyChanged(nameof(StatusMsg));
        }

        private async void OnHealthyClicked(object sender, EventArgs e)
        {
            // 开始动画
            await StartLoadingAnimation();
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            string foodName = "";
            string reason = "";
            string status = "";
            bool success = false;
            
            // 重置结果
            RecommendResult = string.Empty;
            RecommendFoodName = string.Empty;
            RecommendReason = string.Empty;

            try
            {
                // 1. 获取用户详细饮食历史数据
                var userMealData = await _mealService.GetUserDetailedMealsJsonOptimizedAsync(_userId, 30);
                
                if (string.IsNullOrEmpty(userMealData) || userMealData == "[]")
                {
                    status = "暂无饮食历史，无法提供个性化健康推荐";
                }
                else
                {
                    // 2. 调用LLM服务获取健康推荐
                    var recommendationResult = await _foodRecommendationService.RecommendHealthyFoodAsync(userMealData);
                    
                    System.Diagnostics.Debug.WriteLine($"LLM推荐结果: {recommendationResult}");
                    
                    // 3. 解析推荐结果
                    var parts = recommendationResult.Split('|', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        foodName = parts[0].Trim();
                        reason = parts[1].Trim();
                        success = true;
                        System.Diagnostics.Debug.WriteLine($"解析成功 - 菜名: {foodName}, 理由: {reason}");
                    }
                    else if (parts.Length == 1)
                    {
                        foodName = parts[0].Trim();
                        reason = "营养均衡，有益健康";
                        success = true;
                        System.Diagnostics.Debug.WriteLine($"部分解析成功 - 菜名: {foodName}");
                    }
                    else
                    {
                        status = "推荐服务返回格式异常";
                        System.Diagnostics.Debug.WriteLine($"解析失败 - 原始结果: {recommendationResult}");
                    }
                }
            }
            catch (Exception ex)
            {
                status = $"健康推荐失败：{ex.Message}";
                System.Diagnostics.Debug.WriteLine($"健康推荐异常: {ex}");
            }

            // 确保动画至少播放完整时间
            var elapsed = stopwatch.ElapsedMilliseconds;
            if (elapsed < MinAnimationDuration)
            {
                await Task.Delay(MinAnimationDuration - (int)elapsed);
            }

            // 停止加载动画
            await StopLoadingAnimation();

            // 更新结果
            if (success)
            {
                RecommendFoodName = foodName;
                RecommendReason = reason;
                StatusMsg = string.Empty;
                await ShowResultAnimation();
            }
            else
            {
                RecommendFoodName = string.Empty;
                RecommendReason = string.Empty;
                StatusMsg = status;
                HasResult = false;
                OnPropertyChanged(nameof(HasResult));
            }

            OnPropertyChanged(nameof(RecommendFoodName));
            OnPropertyChanged(nameof(RecommendReason));
            OnPropertyChanged(nameof(StatusMsg));
        }

        private async Task StartLoadingAnimation()
        {
            // 隐藏结果，显示动画
            HasResult = false;
            IsAnimating = true;
            OnPropertyChanged(nameof(HasResult));
            OnPropertyChanged(nameof(IsAnimating));

            // 启动各种动画
            _ = Task.Run(async () =>
            {
                while (IsAnimating)
                {
                    // 主线程执行动画
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        // 美食图标脉冲动画
                        await FoodIcon.ScaleTo(1.2, 500);
                        await FoodIcon.ScaleTo(1.0, 500);
                        
                        // 外圈旋转
                        await OuterRing.RotateTo(360, 2000);
                        OuterRing.Rotation = 0;
                    });
                }
            });

            // 内圈反向旋转
            _ = Task.Run(async () =>
            {
                while (IsAnimating)
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await InnerRing.RotateTo(-360, 1500);
                        InnerRing.Rotation = 0;
                    });
                }
            });

            // 加载文字闪烁
            _ = Task.Run(async () =>
            {
                var messages = new[]
                {
                    "🔮 正在为您寻找美味...",
                    "👨‍🍳 大厨正在思考中...",
                    "🎯 锁定最佳选择...",
                    "✨ 即将揭晓答案..."
                };
                int index = 0;
                
                while (IsAnimating)
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        LoadingText.Text = messages[index];
                        await LoadingText.FadeTo(0.5, 250);
                        await LoadingText.FadeTo(1.0, 250);
                    });
                    index = (index + 1) % messages.Length;
                    await Task.Delay(800);
                }
            });
        }

        private async Task StopLoadingAnimation()
        {
            IsAnimating = false;
            OnPropertyChanged(nameof(IsAnimating));
            
            // 重置所有动画元素
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                FoodIcon.Scale = 1.0;
                OuterRing.Rotation = 0;
                InnerRing.Rotation = 0;
                LoadingText.Opacity = 1.0;
            });
        }

        private async Task ShowResultAnimation()
        {
            HasResult = true;
            OnPropertyChanged(nameof(HasResult));

            await Task.Delay(100); // 确保UI更新

            // 结果容器入场动画
            ResultContainer.Scale = 0.1;
            ResultContainer.Opacity = 0;

            // 重置星星透明度
            Star1.Opacity = 0;
            Star2.Opacity = 0;
            Star3.Opacity = 0;
            CelebrationIcon.Opacity = 0;

            // 庆祝动画序列
            var celebrationTask = Task.Run(async () =>
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    // 1. 容器放大入场
                    await Task.WhenAll(
                        ResultContainer.ScaleTo(1.0, 600, Easing.BounceOut),
                        ResultContainer.FadeTo(1.0, 600)
                    );

                    // 2. 庆祝图标动画
                    await CelebrationIcon.FadeTo(1.0, 300);
                    await CelebrationIcon.ScaleTo(1.5, 200);
                    await CelebrationIcon.ScaleTo(1.0, 200);

                    // 3. 菜名特效
                    RecommendFoodNameLabel.Scale = 0.5;
                    await RecommendFoodNameLabel.ScaleTo(1.2, 400, Easing.BounceOut);
                    await RecommendFoodNameLabel.ScaleTo(1.0, 200);

                    // 4. 推荐理由渐入
                    RecommendReasonLabel.Opacity = 0;
                    await RecommendReasonLabel.FadeTo(1.0, 500);

                    // 5. 星星依次出现
                    await Star1.FadeTo(1.0, 200);
                    await Task.Delay(100);
                    await Star2.FadeTo(1.0, 200);
                    await Task.Delay(100);
                    await Star3.FadeTo(1.0, 200);

                    // 6. 整体框架闪烁效果
                    for (int i = 0; i < 2; i++)
                    {
                        ResultFrame.BackgroundColor = Color.FromArgb("#FFF0F8FF");
                        await Task.Delay(150);
                        ResultFrame.BackgroundColor = Colors.White;
                        await Task.Delay(150);
                    }
                });
            });

            await celebrationTask;
        }

        // private void OnHealthyClicked(object sender, EventArgs e)
        // {
        //     // 健康推荐逻辑（可根据需求实现）
        //     StatusMsg = "健康推荐功能暂未实现";
        //     OnPropertyChanged(nameof(StatusMsg));
        // }

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