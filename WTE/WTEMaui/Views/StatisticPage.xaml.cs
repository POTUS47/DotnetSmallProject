using DataAccessLib.Services;
using Microsoft.Maui.Controls;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace WTEMaui.Views
{
    public partial class StatisticPage : ContentPage, INotifyPropertyChanged
    {
        private readonly MealService _mealService;
        private readonly FoodService _foodService;
        private readonly TagService _tagService;
        private readonly int _userId;

        public string UserName { get; set; } = "POTUS 47";

        private DateTime _startDate = DateTime.Today.AddDays(-30);
        public DateTime StartDate
        {
            get => _startDate;
            set
            {
                if (_startDate != value)
                {
                    _startDate = value;
                    OnPropertyChanged(nameof(StartDate));
                }
            }
        }

        private DateTime _endDate = DateTime.Today;
        public DateTime EndDate
        {
            get => _endDate;
            set
            {
                if (_endDate != value)
                {
                    _endDate = value;
                    OnPropertyChanged(nameof(EndDate));
                }
            }
        }

        private bool _isLoading = false;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    OnPropertyChanged(nameof(IsLoading));
                }
            }
        }

        private string _chartTitle = "选择统计类型";
        public string ChartTitle
        {
            get => _chartTitle;
            set
            {
                if (_chartTitle != value)
                {
                    _chartTitle = value;
                    OnPropertyChanged(nameof(ChartTitle));
                }
            }
        }

        private string _xAxisTitle = "时间";
        public string XAxisTitle
        {
            get => _xAxisTitle;
            set
            {
                if (_xAxisTitle != value)
                {
                    _xAxisTitle = value;
                    OnPropertyChanged(nameof(XAxisTitle));
                }
            }
        }

        private bool _showPieChart = false;
        public bool ShowPieChart
        {
            get => _showPieChart;
            set
            {
                if (_showPieChart != value)
                {
                    _showPieChart = value;
                    OnPropertyChanged(nameof(ShowPieChart));
                }
            }
        }

        private bool _showColumnChart = false;
        public bool ShowColumnChart
        {
            get => _showColumnChart;
            set
            {
                if (_showColumnChart != value)
                {
                    _showColumnChart = value;
                    OnPropertyChanged(nameof(ShowColumnChart));
                }
            }
        }

        private bool _showSummary = false;
        public bool ShowSummary
        {
            get => _showSummary;
            set
            {
                if (_showSummary != value)
                {
                    _showSummary = value;
                    OnPropertyChanged(nameof(ShowSummary));
                }
            }
        }

        private string _summaryText = "";
        public string SummaryText
        {
            get => _summaryText;
            set
            {
                if (_summaryText != value)
                {
                    _summaryText = value;
                    OnPropertyChanged(nameof(SummaryText));
                }
            }
        }

        public ObservableCollection<ChartDataModel> ChartData { get; set; } = new();

        // 按钮颜色状态
        public string FoodStatColor => _currentStatType == StatType.Food ? "#4CAF50" : "#9E9E9E";
        public string TagStatColor => _currentStatType == StatType.Tag ? "#4CAF50" : "#9E9E9E";
        public string WeeklyStatColor => _currentPeriodType == PeriodType.Weekly ? "#2196F3" : "#9E9E9E";
        public string MonthlyStatColor => _currentPeriodType == PeriodType.Monthly ? "#2196F3" : "#9E9E9E";

        private StatType _currentStatType = StatType.Food;
        private PeriodType _currentPeriodType = PeriodType.Weekly;

        public StatisticPage(MealService mealService, FoodService foodService, TagService tagService)
        {
            InitializeComponent();
            _mealService = mealService;
            _foodService = foodService;
            _tagService = tagService;
            _userId = App.CurrentUser?.UserId ?? 1;
            UserName = App.CurrentUser?.Username ?? "未登录用户";

            BindingContext = this;
        }

        private async void OnFoodStatClicked(object sender, EventArgs e)
        {
            _currentStatType = StatType.Food;
            UpdateButtonColors();
            await LoadStatisticData();
        }

        private async void OnTagStatClicked(object sender, EventArgs e)
        {
            _currentStatType = StatType.Tag;
            UpdateButtonColors();
            await LoadStatisticData();
        }

        private async void OnWeeklyStatClicked(object sender, EventArgs e)
        {
            _currentPeriodType = PeriodType.Weekly;
            UpdateButtonColors();
            await LoadStatisticData();
        }

        private async void OnMonthlyStatClicked(object sender, EventArgs e)
        {
            _currentPeriodType = PeriodType.Monthly;
            UpdateButtonColors();
            await LoadStatisticData();
        }

        private async void OnRefreshDataClicked(object sender, EventArgs e)
        {
            await LoadStatisticData();
        }

        private void UpdateButtonColors()
        {
            OnPropertyChanged(nameof(FoodStatColor));
            OnPropertyChanged(nameof(TagStatColor));
            OnPropertyChanged(nameof(WeeklyStatColor));
            OnPropertyChanged(nameof(MonthlyStatColor));
        }

        private async Task LoadStatisticData()
        {
            if (IsLoading) return;

            IsLoading = true;
            try
            {
                ChartData.Clear();
                
                if (_currentPeriodType == PeriodType.Weekly)
                {
                    await LoadWeeklyData();
                }
                else
                {
                    await LoadMonthlyData();
                }

                UpdateChartVisibility();
                GenerateSummary();
            }
            catch (Exception ex)
            {
                await DisplayAlert("错误", $"加载统计数据失败: {ex.Message}", "确定");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadWeeklyData()
        {
            var startDateOnly = DateOnly.FromDateTime(StartDate);
            var endDateOnly = DateOnly.FromDateTime(EndDate);
            
            if (_currentStatType == StatType.Food)
            {
                ChartTitle = "每周食物统计";
                XAxisTitle = "周次";
                
                // 获取时间范围内的所有餐食记录
                var meals = await _mealService.GetUserMealsByDateRangeAsync(_userId, startDateOnly, endDateOnly);
                
                // 按周分组统计食物
                var weeklyFoodStats = meals
                    .GroupBy(m => GetWeekOfYear(m.MealDate))
                    .SelectMany(weekGroup => weekGroup
                        .SelectMany(m => m.MealFoodImages.Select(mfi => mfi.Food.Name))
                        .GroupBy(foodName => foodName)
                        .Select(foodGroup => new ChartDataModel
                        {
                            Name = $"第{weekGroup.Key}周-{foodGroup.Key}",
                            Count = foodGroup.Count()
                        }))
                    .OrderByDescending(x => x.Count)
                    .Take(10)
                    .ToList();

                foreach (var item in weeklyFoodStats)
                {
                    ChartData.Add(item);
                }
            }
            else
            {
                ChartTitle = "每周标签统计";
                XAxisTitle = "周次";
                
                // 获取时间范围内的所有餐食记录
                var meals = await _mealService.GetUserMealsByDateRangeAsync(_userId, startDateOnly, endDateOnly);
                
                // 按周分组统计标签
                var weeklyTagStats = meals
                    .GroupBy(m => GetWeekOfYear(m.MealDate))
                    .SelectMany(weekGroup => weekGroup
                        .SelectMany(m => m.MealFoodTags.Select(mft => mft.Tag.TagName))
                        .GroupBy(tagName => tagName)
                        .Select(tagGroup => new ChartDataModel
                        {
                            Name = $"第{weekGroup.Key}周-{tagGroup.Key}",
                            Count = tagGroup.Count()
                        }))
                    .OrderByDescending(x => x.Count)
                    .Take(10)
                    .ToList();

                foreach (var item in weeklyTagStats)
                {
                    ChartData.Add(item);
                }
            }
        }

        private async Task LoadMonthlyData()
        {
            var startDateOnly = DateOnly.FromDateTime(StartDate);
            var endDateOnly = DateOnly.FromDateTime(EndDate);
            
            if (_currentStatType == StatType.Food)
            {
                ChartTitle = "每月食物统计";
                XAxisTitle = "月份";
                
                // 获取时间范围内的所有餐食记录
                var meals = await _mealService.GetUserMealsByDateRangeAsync(_userId, startDateOnly, endDateOnly);
                
                // 按月分组统计食物
                var monthlyFoodStats = meals
                    .GroupBy(m => new { m.MealDate.Year, m.MealDate.Month })
                    .SelectMany(monthGroup => monthGroup
                        .SelectMany(m => m.MealFoodImages.Select(mfi => mfi.Food.Name))
                        .GroupBy(foodName => foodName)
                        .Select(foodGroup => new ChartDataModel
                        {
                            Name = $"{monthGroup.Key.Year}-{monthGroup.Key.Month:00}-{foodGroup.Key}",
                            Count = foodGroup.Count()
                        }))
                    .OrderByDescending(x => x.Count)
                    .Take(15)
                    .ToList();

                foreach (var item in monthlyFoodStats)
                {
                    ChartData.Add(item);
                }
            }
            else
            {
                ChartTitle = "每月标签统计";
                XAxisTitle = "月份";
                
                // 获取时间范围内的所有餐食记录
                var meals = await _mealService.GetUserMealsByDateRangeAsync(_userId, startDateOnly, endDateOnly);
                
                // 按月分组统计标签
                var monthlyTagStats = meals
                    .GroupBy(m => new { m.MealDate.Year, m.MealDate.Month })
                    .SelectMany(monthGroup => monthGroup
                        .SelectMany(m => m.MealFoodTags.Select(mft => mft.Tag.TagName))
                        .GroupBy(tagName => tagName)
                        .Select(tagGroup => new ChartDataModel
                        {
                            Name = $"{monthGroup.Key.Year}-{monthGroup.Key.Month:00}-{tagGroup.Key}",
                            Count = tagGroup.Count()
                        }))
                    .OrderByDescending(x => x.Count)
                    .Take(15)
                    .ToList();

                foreach (var item in monthlyTagStats)
                {
                    ChartData.Add(item);
                }
            }
        }

        private void UpdateChartVisibility()
        {
            if (ChartData.Count > 0)
            {
                // 如果数据项较少，使用饼图；否则使用柱状图
                if (ChartData.Count <= 8)
                {
                    ShowPieChart = true;
                    ShowColumnChart = false;
                }
                else
                {
                    ShowPieChart = false;
                    ShowColumnChart = true;
                }
                ShowSummary = true;
            }
            else
            {
                ShowPieChart = false;
                ShowColumnChart = false;
                ShowSummary = false;
            }
        }

        private void GenerateSummary()
        {
            if (ChartData.Count == 0)
            {
                SummaryText = "选定时间范围内没有数据";
                return;
            }

            var totalCount = ChartData.Sum(x => x.Count);
            var topItem = ChartData.OrderByDescending(x => x.Count).First();
            var itemType = _currentStatType == StatType.Food ? "食物" : "标签";
            var period = _currentPeriodType == PeriodType.Weekly ? "每周" : "每月";

            SummaryText = $"在选定的时间范围内，{period}{itemType}统计显示：\n" +
                         $"• 总计记录数：{totalCount}\n" +
                         $"• 最常见的项目：{topItem.Name}（{topItem.Count}次）\n" +
                         $"• 统计项目总数：{ChartData.Count}";
        }

        private int GetWeekOfYear(DateOnly date)
        {
            var culture = System.Globalization.CultureInfo.CurrentCulture;
            var calendar = culture.Calendar;
            return calendar.GetWeekOfYear(date.ToDateTime(TimeOnly.MinValue), 
                culture.DateTimeFormat.CalendarWeekRule, 
                culture.DateTimeFormat.FirstDayOfWeek);
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

        public new event PropertyChangedEventHandler PropertyChanged;
        protected new void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public class ChartDataModel
        {
            public string Name { get; set; }
            public int Count { get; set; }
        }

        private enum StatType
        {
            Food,
            Tag
        }

        private enum PeriodType
        {
            Weekly,
            Monthly
        }
    }
}