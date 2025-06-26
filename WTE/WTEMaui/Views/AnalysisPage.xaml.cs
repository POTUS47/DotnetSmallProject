using DataAccessLib.Services;
using LLMLib;
using Microsoft.Maui.Controls;
using Syncfusion.Maui.Calendar;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using WTEMaui.Services;
using Syncfusion.Maui.Charts;

namespace WTEMaui.Views
{
    public partial class AnalysisPage : ContentPage, INotifyPropertyChanged
    {
        public ObservableCollection<DailyStatVM> DailyStats { get; set; } = new();
        public string UserName { get; set; } = "POTUS 47";
        
        private string _healthAdvice = "";
        public string HealthAdvice
        {
            get => _healthAdvice;
            set
            {
                if (_healthAdvice != value)
                {
                    _healthAdvice = value;
                    OnPropertyChanged(nameof(HealthAdvice));
                    OnPropertyChanged(nameof(ShowHealthAdvice));
                }
            }
        }

        private bool _isLoadingAdvice = false;
        public bool IsLoadingAdvice
        {
            get => _isLoadingAdvice;
            set
            {
                if (_isLoadingAdvice != value)
                {
                    _isLoadingAdvice = value;
                    OnPropertyChanged(nameof(IsLoadingAdvice));
                    OnPropertyChanged(nameof(ShowGetAdviceButton));
                }
            }
        }

        public bool ShowGetAdviceButton => !IsLoadingAdvice && string.IsNullOrEmpty(HealthAdvice);
        public bool ShowHealthAdvice => !string.IsNullOrEmpty(HealthAdvice);

        // 添加饮食健康分析相关属性
        private string _dietHealthAdvice = "";
        public string DietHealthAdvice
        {
            get => _dietHealthAdvice;
            set
            {
                if (_dietHealthAdvice != value)
                {
                    _dietHealthAdvice = value;
                    OnPropertyChanged(nameof(DietHealthAdvice));
                    OnPropertyChanged(nameof(ShowDietHealthAdvice));
                }
            }
        }

        private bool _isLoadingDietAnalysis = false;
        public bool IsLoadingDietAnalysis
        {
            get => _isLoadingDietAnalysis;
            set
            {
                if (_isLoadingDietAnalysis != value)
                {
                    _isLoadingDietAnalysis = value;
                    OnPropertyChanged(nameof(IsLoadingDietAnalysis));
                    OnPropertyChanged(nameof(ShowGetDietAnalysisButton));
                }
            }
        }

        public bool ShowGetDietAnalysisButton => !IsLoadingDietAnalysis && string.IsNullOrEmpty(DietHealthAdvice);
        public bool ShowDietHealthAdvice => !string.IsNullOrEmpty(DietHealthAdvice);

        private readonly MealService _mealService;
        private readonly HealthAnalysisService _healthAnalysisService;

        private DateTime _selectedDate = DateTime.Today;
        public DateTime SelectedDate
        {
            get => _selectedDate;
            set
            {
                if (_selectedDate != value)
                {
                    _selectedDate = value;
                    OnPropertyChanged(nameof(SelectedDate));
                    UpdateSelectedDayFoods();
                }
            }
        }

        public ObservableCollection<string> SelectedDayFoods { get; set; } = new();

        private readonly AnalysisService _analysisService;
        private readonly int _userId; // 移除硬编码，改为从当前用户获取
        private DateOnly _startDate;
        private DateOnly _endDate;

        private readonly TagStatisticsService _tagStatisticsService;

        public AnalysisPage(
            HealthAnalysisService healthAnalysisService,
            MealService mealService,
            AnalysisService analysisService,
            TagStatisticsService tagStatisticsService)
        {
            try
            {
                InitializeComponent();
                _healthAnalysisService = healthAnalysisService;
                _mealService = mealService;
                _analysisService = analysisService;
                _tagStatisticsService = tagStatisticsService;
                
                _userId = App.CurrentUser?.UserId ?? 1; // 如果未登录，默认使用ID=1
                UserName = App.CurrentUser?.Username ?? "未登录用户";
                BindingContext = this;
                FoodCalendar.Identifier = CalendarIdentifier.Gregorian;

                _startDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-30));
                _endDate = DateOnly.FromDateTime(DateTime.Today);
                Task.Run(async () =>
                {
                    await LoadData();
                });
            }
            catch (Exception ex)
            {
                Application.Current?.MainPage?.DisplayAlert("分析页初始化异常", ex.ToString(), "确定");
                throw;
            }
        }

        private async void OnGetAdviceClicked(object sender, EventArgs e)
        {
            IsLoadingAdvice = true;
            HealthAdvice = ""; // 清空之前的内容
            try
            {
                await AnalyzeMealTimeStream(); // 修复方法名称
            }
            catch (Exception ex)
            {
                await DisplayAlert("错误", $"获取健康建议失败: {ex.Message}", "确定");
                HealthAdvice = "获取健康建议失败，请稍后重试";
            }
            finally
            {
                IsLoadingAdvice = false;
            }
        }

        // 添加饮食健康分析按钮点击事件
        private async void OnGetDietAnalysisClicked(object sender, EventArgs e)
        {
            IsLoadingDietAnalysis = true;
            DietHealthAdvice = ""; // 清空之前的内容
            try
            {
                await AnalyzeDietHealthStream();
            }
            catch (Exception ex)
            {
                await DisplayAlert("错误", $"获取饮食健康分析失败: {ex.Message}", "确定");
                DietHealthAdvice = "获取饮食健康分析失败，请稍后重试";
            }
            finally
            {
                IsLoadingDietAnalysis = false;
            }
        }

        // 添加缺失的用餐时间分析流式方法
        private async Task AnalyzeMealTimeStream()
        {
            var history = await _mealService.GetUserMealsJsonAsync(_userId);
            try
            {
                Console.WriteLine("正在调用大模型分析饮食时间数据...");

                // 使用流式输出
                await _healthAnalysisService.AnalyzeMealTimeStreamAsync(history, (content) =>
                {
                    // 在主线程更新UI
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        HealthAdvice += content;
                    });
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"错误: {ex.Message}");
                throw;
            }
        }

        // 添加饮食健康分析方法
        private async Task AnalyzeDietHealthStream()
        {
            // 使用优化版本的方法
            var detailedData = await _mealService.GetUserDetailedMealsJsonOptimizedAsync(_userId);
            try
            {
                Console.WriteLine("正在调用大模型分析饮食健康数据...");

                // 使用流式输出
                await _healthAnalysisService.AnalyzeDietHealthStreamAsync(detailedData, (content) =>
                {
                    // 在主线程更新UI
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        DietHealthAdvice += content;
                    });
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"错误: {ex.Message}");
                throw;
            }
        }

        // 保留原有的非流式方法作为备用
        private async Task AnalysisLLM()
        {
            var history = await _mealService.GetUserMealsJsonAsync(_userId);
            try
            {
                Console.WriteLine("正在调用大模型分析饮食数据...");

                string advice = await _healthAnalysisService.AnalyzeMealTimeAsync(history);
                HealthAdvice = advice;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"错误: {ex.Message}");
                throw;
            }
        }

        // 保留原有的非流式方法作为备用
        private async Task AnalyzeDietHealth()
        {
            // 使用优化版本的方法
            var detailedData = await _mealService.GetUserDetailedMealsJsonOptimizedAsync(_userId);
            try
            {
                Console.WriteLine("正在调用大模型分析饮食健康数据...");

                string advice = await _healthAnalysisService.AnalyzeDietHealthAsync(detailedData);
                DietHealthAdvice = advice;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"错误: {ex.Message}");
                throw;
            }
        }

        private async Task LoadData()
        {
            var stats = await _analysisService.GetUserDailyStatsAsync(_userId, _startDate, _endDate);
            DailyStats.Clear();
            foreach (var s in stats)
            {
                DailyStats.Add(new DailyStatVM
                {
                    Date = s.Date.ToString("yyyy-MM-dd"),
                    FoodsString = s.Foods.Count > 0 ? string.Join(", ", s.Foods) : "无记录",
                    TotalCalories = s.TotalCalories,
                    TotalProtein = s.TotalProtein
                });
            }
            UpdateSelectedDayFoods();
        }

        private void UpdateSelectedDayFoods()
        {
            SelectedDayFoods.Clear();
            var dateStr = SelectedDate.ToString("yyyy-MM-dd");
            var stat = DailyStats.FirstOrDefault(d => d.Date == dateStr);
            if (stat != null && stat.FoodsString != "无记录")
            {
                foreach (var food in stat.FoodsString.Split(", "))
                {
                    SelectedDayFoods.Add(food);
                }
            }
            else
            {
                SelectedDayFoods.Add("暂无数据");
            }
        }

        private void OnCalendarSelectionChanged(object sender, CalendarSelectionChangedEventArgs e)
        {
            if (e.NewValue is DateTime date)
            {
                SelectedDate = date;
            }
        }

        public new event PropertyChangedEventHandler PropertyChanged;
        protected new void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private async void OnSettingsClicked(object sender, EventArgs e)
        {
            try
            {
                await Shell.Current.GoToAsync(nameof(SettingsPage));
            }
            catch (Exception ex)
            {
                await DisplayAlert("错误", "无法打开设置页面", "确定");
            }
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            LoadTagStatistics();
        }

        private void LoadTagStatistics()
        {
            try
            {
                var startDate = _startDate.ToDateTime(TimeOnly.MinValue);
                var endDate = _endDate.ToDateTime(TimeOnly.MinValue);
                var stats = _tagStatisticsService.GetUserTagStatistics(_userId, startDate, endDate);
                
                if (stats != null && stats.Any())
                {
                    var chartData = stats.Select(s => new TagStatisticData
                    {
                        TagName = s.TagName,
                        Percentage = s.Percentage * 100
                    }).ToList();

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        pieSeries.ItemsSource = chartData;
                    });
                }
                else
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await DisplayAlert("提示", "当前时间段没有标签数据", "确定");
                    });
                }
            }
            catch (Exception ex)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    if (ex is FileNotFoundException)
                    {
                        await DisplayAlert("错误", "找不到必要的DLL文件，请确保应用程序完整性", "确定");
                    }
                    else if (ex is DllNotFoundException)
                    {
                        await DisplayAlert("错误", "无法加载必要的DLL文件，请确保应用程序完整性", "确定");
                    }
                    else
                    {
                        await DisplayAlert("错误", $"加载标签统计数据失败: {ex.Message}", "确定");
                    }
                    System.Diagnostics.Debug.WriteLine($"LoadTagStatistics error: {ex}");
                });
            }
        }

        private void UpdateChartData(DateTime startDate, DateTime endDate)
        {
            try
            {
                var stats = _tagStatisticsService.GetUserTagStatistics(_userId, startDate, endDate);
                
                if (stats != null)
                {
                    if (stats.Any())
                    {
                        var chartData = stats.Select(s => new TagStatisticData
                        {
                            TagName = $"{s.TagName} ({s.Count})",
                            Percentage = s.Percentage
                        }).ToList();

                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            try
                            {
                                if (pieSeries != null)
                                {
                                    pieSeries.ItemsSource = null;
                                    pieSeries.ItemsSource = chartData;
                                }
                                else
                                {
                                    DisplayAlert("错误", "pieSeries 为 null", "确定");
                                }
                            }
                            catch (Exception ex)
                            {
                                DisplayAlert("错误", $"更新图表数据源时出错: {ex.Message}", "确定");
                            }
                        });
                    }
                    else
                    {
                        MainThread.BeginInvokeOnMainThread(async () =>
                        {
                            await DisplayAlert("提示", 
                                $"当前时间段（{startDate:yyyy-MM-dd} 至 {endDate:yyyy-MM-dd}）没有标签数据", 
                                "确定");
                        });
                    }
                }
                else
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await DisplayAlert("提示", 
                            $"GetUserTagStatistics 返回了 null", 
                            "确定");
                    });
                }
            }
            catch (Exception ex)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await DisplayAlert("错误", 
                        $"更新图表数据失败: {ex.Message}\n\n{ex.StackTrace}", 
                        "确定");
                });
            }
        }

        private void OnWeeklyStatsClicked(object sender, EventArgs e)
        {
            try
            {
                // 获取本周一作为开始日期
                var today = DateTime.Today;
                var monday = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);
                var sunday = monday.AddDays(6); // 周日
                Console.WriteLine($"切换到周统计 - 从 {monday:yyyy-MM-dd} 到 {sunday:yyyy-MM-dd}");
                UpdateChartData(monday, sunday);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"周统计出错: {ex}");
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await DisplayAlert("错误", $"加载周统计数据失败: {ex.Message}", "确定");
                });
            }
        }

        private void OnMonthlyStatsClicked(object sender, EventArgs e)
        {
            try
            {
                // 获取本月1号和最后一天
                var firstDayOfMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);
                Console.WriteLine($"切换到月统计 - 从 {firstDayOfMonth:yyyy-MM-dd} 到 {lastDayOfMonth:yyyy-MM-dd}");
                UpdateChartData(firstDayOfMonth, lastDayOfMonth);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"月统计出错: {ex}");
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await DisplayAlert("错误", $"加载月统计数据失败: {ex.Message}", "确定");
                });
            }
        }

        public class DailyStatVM
        {
            public string Date { get; set; }
            public string FoodsString { get; set; }
            public double TotalCalories { get; set; }
            public double TotalProtein { get; set; }
        }
    }

    public class TagStatisticData
    {
        public string TagName { get; set; } = "";
        public double Percentage { get; set; }
    }
}