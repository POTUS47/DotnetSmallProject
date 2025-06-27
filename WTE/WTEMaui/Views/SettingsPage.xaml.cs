using DataAccessLib.Services;
using System.ComponentModel;
using System.Text.Json;
using System.Collections.ObjectModel;

namespace WTEMaui.Views
{
    public partial class SettingsPage : ContentPage, INotifyPropertyChanged
    {
        private readonly UserService _userService;
        
        // 私有字段
        private string _selectedHealthGoal = "";
        
        // 绑定属性
        public string UserName { get; set; } = "";
        public string UserEmail { get; set; } = "";
        public string Height { get; set; } = "";
        public string Weight { get; set; } = "";
        public string SelectedHealthGoal 
        { 
            get => _selectedHealthGoal; 
            set 
            { 
                _selectedHealthGoal = value; 
                IsCustomHealthGoal = value == "其他";
                OnPropertyChanged(nameof(SelectedHealthGoal));
                OnPropertyChanged(nameof(IsCustomHealthGoal));
            } 
        }
        public string CustomHealthGoal { get; set; } = "";
        public bool IsCustomHealthGoal { get; set; } = false;
        public ObservableCollection<string> HealthGoalOptions { get; set; } = new ObservableCollection<string>
        {
            "减重", "增重", "保持健康", "增肌", "控制血糖", "其他"
        };
        public AllergiesModel Allergies { get; set; } = new AllergiesModel();
        public string OtherAllergiesText { get; set; } = "";

        public SettingsPage() : this(WTEMaui.App.Services?.GetService<UserService>())
        {
        }

        public SettingsPage(UserService userService)
        {
            InitializeComponent();
            // 双重确保导航栏隐藏
            Shell.SetNavBarIsVisible(this, false);
            _userService = userService;
            // 先初始化所有属性
            UserName = "";
            UserEmail = "";
            Height = "";
            Weight = "";
            SelectedHealthGoal = "";
            CustomHealthGoal = "";
            IsCustomHealthGoal = false;
            Allergies = new AllergiesModel();
            OtherAllergiesText = "";
            // 再设置BindingContext
            BindingContext = this;
            // 设置属性变化监听
            PropertyChanged += OnPropertyChanged;
            LoadUserData();
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // 现在SelectedHealthGoal的变化直接在属性setter中处理
        }

        private async void LoadUserData()
        {
            try
            {
                if (App.CurrentUser == null)
                {
                    await DisplayAlert("错误", "请先登录", "确定");
                    return;
                }

                LoadingIndicator.IsVisible = true;
                LoadingIndicator.IsRunning = true;

                // 设置基本信息
                UserName = App.CurrentUser.Username;
                UserEmail = App.CurrentUser.Email;
                OnPropertyChanged(nameof(UserName));
                OnPropertyChanged(nameof(UserEmail));

                // 加载健康数据
                var healthData = await _userService.GetHealthDataAsync(App.CurrentUser.UserId);
                
                Height = healthData.Height?.ToString() ?? "";
                Weight = healthData.Weight?.ToString() ?? "";
                
                // 处理健康目标
                if (!string.IsNullOrEmpty(healthData.HealthGoal))
                {
                    var predefinedGoals = new[] { "减重", "增重", "保持健康", "增肌", "控制血糖" };
                    if (predefinedGoals.Contains(healthData.HealthGoal))
                    {
                        SelectedHealthGoal = healthData.HealthGoal;
                        IsCustomHealthGoal = false;
                    }
                    else
                    {
                        SelectedHealthGoal = "其他";
                        CustomHealthGoal = healthData.HealthGoal;
                        IsCustomHealthGoal = true;
                    }
                }
                else
                {
                    SelectedHealthGoal = "";
                    IsCustomHealthGoal = false;
                }
                
                // 处理过敏源
                if (!string.IsNullOrEmpty(healthData.Allergies))
                {
                    try
                    {
                        var allergiesData = JsonSerializer.Deserialize<AllergiesModel>(healthData.Allergies);
                        if (allergiesData != null)
                        {
                            Allergies = allergiesData;
                            OtherAllergiesText = allergiesData.OtherText ?? "";
                        }
                    }
                    catch
                    {
                        // 如果解析失败，可能是旧格式，直接作为其他过敏源
                        Allergies.Other = true;
                        OtherAllergiesText = healthData.Allergies;
                    }
                }

                OnPropertyChanged(nameof(Height));
                OnPropertyChanged(nameof(Weight));
                OnPropertyChanged(nameof(SelectedHealthGoal));
                OnPropertyChanged(nameof(CustomHealthGoal));
                OnPropertyChanged(nameof(IsCustomHealthGoal));
                OnPropertyChanged(nameof(Allergies));
                OnPropertyChanged(nameof(OtherAllergiesText));
            }
            catch (Exception ex)
            {
                await DisplayAlert("错误", $"加载用户数据失败: {ex.Message}", "确定");
            }
            finally
            {
                LoadingIndicator.IsVisible = false;
                LoadingIndicator.IsRunning = false;
            }
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            try
            {
                if (App.CurrentUser == null)
                {
                    await DisplayAlert("错误", "请先登录", "确定");
                    return;
                }

                LoadingIndicator.IsVisible = true;
                LoadingIndicator.IsRunning = true;

                // 解析身高体重
                decimal? height = null;
                decimal? weight = null;
                
                if (decimal.TryParse(Height, out var h))
                    height = h;
                if (decimal.TryParse(Weight, out var w))
                    weight = w;

                // 处理健康目标
                string healthGoal = "";
                if (SelectedHealthGoal == "其他")
                {
                    healthGoal = CustomHealthGoal?.Trim() ?? "";
                }
                else if (!string.IsNullOrEmpty(SelectedHealthGoal))
                {
                    healthGoal = SelectedHealthGoal;
                }

                // 处理过敏源
                string allergies = "";
                var selectedAllergies = new List<string>();
                
                if (Allergies.Peanut) selectedAllergies.Add("花生");
                if (Allergies.Nuts) selectedAllergies.Add("坚果");
                if (Allergies.Seafood) selectedAllergies.Add("海鲜");
                if (Allergies.Eggs) selectedAllergies.Add("鸡蛋");
                if (Allergies.Milk) selectedAllergies.Add("牛奶");
                if (Allergies.Soy) selectedAllergies.Add("大豆");
                if (Allergies.Wheat) selectedAllergies.Add("小麦");
                
                if (Allergies.Other && !string.IsNullOrEmpty(OtherAllergiesText))
                {
                    selectedAllergies.Add(OtherAllergiesText.Trim());
                }

                if (selectedAllergies.Any())
                {
                    var allergiesModel = new AllergiesModel
                    {
                        Peanut = Allergies.Peanut,
                        Nuts = Allergies.Nuts,
                        Seafood = Allergies.Seafood,
                        Eggs = Allergies.Eggs,
                        Milk = Allergies.Milk,
                        Soy = Allergies.Soy,
                        Wheat = Allergies.Wheat,
                        Other = Allergies.Other,
                        OtherText = OtherAllergiesText?.Trim()
                    };
                    
                    allergies = JsonSerializer.Serialize(allergiesModel);
                }

                // 保存到数据库
                await _userService.UpdateHealthDataAsync(App.CurrentUser.UserId, height, weight, healthGoal, allergies);

                // 更新App.CurrentUser中的信息
                App.CurrentUser.Height = height;
                App.CurrentUser.Weight = weight;
                App.CurrentUser.HealthGoal = healthGoal;
                App.CurrentUser.Allergies = allergies;

                await DisplayAlert("成功", "设置已保存", "确定");
            }
            catch (Exception ex)
            {
                await DisplayAlert("错误", $"保存设置失败: {ex.Message}", "确定");
            }
            finally
            {
                LoadingIndicator.IsVisible = false;
                LoadingIndicator.IsRunning = false;
            }
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            try
            {
                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex)
            {
                await DisplayAlert("错误", "返回失败", "确定");
            }
        }

        private void OnHealthGoalPickerChanged(object sender, EventArgs e)
        {
            if (sender is Picker picker && picker.SelectedItem is string selectedGoal)
            {
                IsCustomHealthGoal = selectedGoal == "其他";
                OnPropertyChanged(nameof(IsCustomHealthGoal));
            }
        }

        public new event PropertyChangedEventHandler PropertyChanged;
        protected new void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // 过敏源模型
    public class AllergiesModel : INotifyPropertyChanged
    {
        private bool _peanut;
        private bool _nuts;
        private bool _seafood;
        private bool _eggs;
        private bool _milk;
        private bool _soy;
        private bool _wheat;
        private bool _other;
        private string? _otherText;

        public bool Peanut 
        { 
            get => _peanut; 
            set 
            { 
                _peanut = value; 
                OnPropertyChanged(nameof(Peanut)); 
            } 
        }
        public bool Nuts 
        { 
            get => _nuts; 
            set 
            { 
                _nuts = value; 
                OnPropertyChanged(nameof(Nuts)); 
            } 
        }
        public bool Seafood 
        { 
            get => _seafood; 
            set 
            { 
                _seafood = value; 
                OnPropertyChanged(nameof(Seafood)); 
            } 
        }
        public bool Eggs 
        { 
            get => _eggs; 
            set 
            { 
                _eggs = value; 
                OnPropertyChanged(nameof(Eggs)); 
            } 
        }
        public bool Milk 
        { 
            get => _milk; 
            set 
            { 
                _milk = value; 
                OnPropertyChanged(nameof(Milk)); 
            } 
        }
        public bool Soy 
        { 
            get => _soy; 
            set 
            { 
                _soy = value; 
                OnPropertyChanged(nameof(Soy)); 
            } 
        }
        public bool Wheat 
        { 
            get => _wheat; 
            set 
            { 
                _wheat = value; 
                OnPropertyChanged(nameof(Wheat)); 
            } 
        }
        public bool Other 
        { 
            get => _other; 
            set 
            { 
                _other = value; 
                OnPropertyChanged(nameof(Other)); 
            } 
        }
        public string? OtherText 
        { 
            get => _otherText; 
            set 
            { 
                _otherText = value; 
                OnPropertyChanged(nameof(OtherText)); 
            } 
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
} 