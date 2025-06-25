using LLMLib;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Media;
using DataAccessLib.Services;

namespace WTEMaui.Views
{
    public partial class DashboardPage : ContentPage
    {
        private readonly ImageRecognitionService _imageService;
        private readonly OssService _ossService;
        private readonly MealService _mealService;
        private readonly FoodService _foodService;
        private readonly TagService _tagService;
        private byte[] _capturedImageData;
        private string _capturedImagePath;
        private string _ossImagePath;
        private string _currentFoodName = "";
        private string _currentFoodTag = "";
        private readonly ILogger<DashboardPage> _logger;

        private string _currentPreviewImageUrl = "";
        private string _currentPreviewFoodName = "";

        // 手动输入相关变量
        private string _selectedFoodName = "";
        private List<DataAccessLib.Models.Tag> _selectedTags = new List<DataAccessLib.Models.Tag>();
        private List<DataAccessLib.Models.Food> _userHistoryFoods = new List<DataAccessLib.Models.Food>();
        private List<DataAccessLib.Models.Tag> _userHistoryTags = new List<DataAccessLib.Models.Tag>();

        public DashboardPage(
            ImageRecognitionService imageService,
            OssService ossService,
            MealService mealService,
            FoodService foodService,
            TagService tagService,
            ILogger<DashboardPage> logger)
        {
            InitializeComponent();
            _imageService = imageService;
            _ossService = ossService;
            _mealService = mealService;
            _foodService = foodService;
            _tagService = tagService;
            _logger = logger;

            // 初始化UI状态
            LoadingIndicator.IsRunning = false;
            ResultFrame.IsVisible = false;
            PreviewImage.IsVisible = false;
            
            // 初始化历史记录
            _ = LoadMealHistoryAsync();
        }

        private async void OnTakePhotoClicked(object sender, EventArgs e)
        {
            try
            {
                // 重置UI状态
                ResultFrame.IsVisible = false;
                LoadingIndicator.IsRunning = true;

                // 检查权限
                if (!await CheckAndRequestPermissions())
                {
                    await DisplayAlert("权限不足", "需要相机权限才能使用此功能", "确定");
                    return;
                }

                // 拍照
                var photo = await MediaPicker.CapturePhotoAsync(new MediaPickerOptions
                {
                    Title = "请拍摄清晰的食物照片"
                });

                if (photo == null) 
                {
                    LoadingIndicator.IsRunning = false;
                    return;
                }

                // 处理选中的照片
                await ProcessSelectedPhotoAsync(photo);
            }
            catch (Exception ex)
            {
                LoadingIndicator.IsRunning = false;
                _logger?.LogError(ex, "拍照失败");
                await DisplayAlert("错误", $"拍照失败: {ex.Message}", "确定");
            }
        }

        /// <summary>
        /// 从相册选择图片
        /// </summary>
        private async void OnSelectFromGalleryClicked(object sender, EventArgs e)
        {
            try
            {
                // 重置UI状态
                ResultFrame.IsVisible = false;
                LoadingIndicator.IsRunning = true;

                // 检查权限
                if (!await CheckAndRequestGalleryPermissions())
                {
                    await DisplayAlert("权限不足", "需要访问相册权限才能使用此功能", "确定");
                    LoadingIndicator.IsRunning = false;
                    return;
                }

                // 从相册选择图片
                var photo = await MediaPicker.PickPhotoAsync(new MediaPickerOptions
                {
                    Title = "请选择食物图片"
                });

                if (photo == null) 
                {
                    LoadingIndicator.IsRunning = false;
                    return;
                }

                // 处理选中的照片
                await ProcessSelectedPhotoAsync(photo);
            }
            catch (Exception ex)
            {
                LoadingIndicator.IsRunning = false;
                _logger?.LogError(ex, "选择图片失败");
                await DisplayAlert("错误", $"选择图片失败: {ex.Message}", "确定");
            }
        }

        /// <summary>
        /// 处理选中的照片（统一处理拍照和相册选择的图片）
        /// </summary>
        private async Task ProcessSelectedPhotoAsync(FileResult photo)
        {
            try
            {
                // 记录照片信息
                _capturedImagePath = photo.FullPath;
                _logger?.LogInformation("照片完整路径: {FullPath}", photo.FullPath);

                // 显示预览
                using (var stream = await photo.OpenReadAsync())
                {
                    _capturedImageData = await ReadFully(stream);
                    _logger?.LogInformation("图片数据大小: {Size} bytes", _capturedImageData.Length);
                    
                    PreviewImage.Source = ImageSource.FromStream(() => new MemoryStream(_capturedImageData));
                    PreviewImage.IsVisible = true;
                }

                // 上传到OSS
                await UploadImageToOssAsync(photo.FullPath, photo.FileName);

                // 自动开始识别
                await StartRecognitionAsync();
            }
            catch (Exception ex)
            {
                LoadingIndicator.IsRunning = false;
                _logger?.LogError(ex, "处理选中照片失败");
                throw;
            }
        }

        /// <summary>
        /// 开始识别食物
        /// </summary>
        private async Task StartRecognitionAsync()
        {
            if (_capturedImageData == null || _capturedImageData.Length == 0)
            {
                await DisplayAlert("提示", "请先拍摄食物照片", "确定");
                return;
            }

            try
            {
                // 调用识别服务
                var result = await _imageService.RecognizeFoodFromImageAsync(_capturedImageData);
                
                // 解析识别结果 (格式: 食物名称/标签)
                ParseRecognitionResult(result);
                
                // 显示结果编辑界面
                UpdateResultDisplay();
                ResultFrame.IsVisible = true;
            }
            catch (Exception ex)
            {
                await DisplayAlert("识别失败", $"识别时出错: {ex.Message}", "确定");
            }
            finally
            {
                LoadingIndicator.IsRunning = false;
            }
        }

        /// <summary>
        /// 解析识别结果
        /// </summary>
        private void ParseRecognitionResult(string result)
        {
            try
            {
                // 假设格式为 "苹果/水果" 或 "苹果"
                if (result.Contains("/"))
                {
                    var parts = result.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    _currentFoodName = parts[0].Trim();
                    _currentFoodTag = parts.Length > 1 ? parts[1].Trim() : "";
                }
                else
                {
                    _currentFoodName = result.Trim();
                    _currentFoodTag = "";
                }
                
                _logger?.LogInformation("解析结果 - 食物名称: {FoodName}, 标签: {FoodTag}", _currentFoodName, _currentFoodTag);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "解析识别结果失败: {Result}", result);
                _currentFoodName = result;
                _currentFoodTag = "";
            }
        }

        /// <summary>
        /// 更新结果显示
        /// </summary>
        private void UpdateResultDisplay()
        {
            FoodNameLabel.Text = string.IsNullOrEmpty(_currentFoodName) ? "未识别到食物" : _currentFoodName;
            FoodTagLabel.Text = string.IsNullOrEmpty(_currentFoodTag) ? "暂无标签" : _currentFoodTag;
            
            FoodNameEntry.Text = _currentFoodName;
            FoodTagEntry.Text = _currentFoodTag;
        }

        // 食物名称编辑相关事件
        private void OnFoodNameTapped(object sender, EventArgs e)
        {
            FoodNameLabel.IsVisible = false;
            FoodNameEntry.IsVisible = true;
            FoodNameEntry.Focus();
        }

        private void OnFoodNameCompleted(object sender, EventArgs e)
        {
            _currentFoodName = FoodNameEntry.Text?.Trim() ?? "";
            FoodNameLabel.Text = string.IsNullOrEmpty(_currentFoodName) ? "未设置食物名称" : _currentFoodName;
            
            FoodNameEntry.IsVisible = false;
            FoodNameLabel.IsVisible = true;
        }

        // 食物标签编辑相关事件
        private void OnFoodTagTapped(object sender, EventArgs e)
        {
            FoodTagLabel.IsVisible = false;
            FoodTagEntry.IsVisible = true;
            FoodTagEntry.Focus();
        }

        private void OnFoodTagCompleted(object sender, EventArgs e)
        {
            _currentFoodTag = FoodTagEntry.Text?.Trim() ?? "";
            FoodTagLabel.Text = string.IsNullOrEmpty(_currentFoodTag) ? "暂无标签" : _currentFoodTag;
            
            FoodTagEntry.IsVisible = false;
            FoodTagLabel.IsVisible = true;
        }

        /// <summary>
        /// 获取或创建食物记录
        /// </summary>
        private async Task<int> GetOrCreateFoodAsync(string foodName)
        {
            try
            {
                return await _foodService.GetOrCreateFoodAsync(foodName);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "获取或创建食物记录失败: {FoodName}", foodName);
                throw new Exception($"处理食物记录失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取或创建标签记录
        /// </summary>
        private async Task<int> GetOrCreateTagAsync(string tagName)
        {
            try
            {
                return await _tagService.GetOrCreateTagAsync(tagName);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "获取或创建标签记录失败: {TagName}", tagName);
                throw new Exception($"处理标签记录失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存餐食记录
        /// </summary>
        private async void OnSaveMealClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_currentFoodName))
            {
                await DisplayAlert("提示", "请设置食物名称", "确定");
                return;
            }

            if (string.IsNullOrEmpty(_ossImagePath))
            {
                await DisplayAlert("提示", "图片尚未上传完成，请稍后再试", "确定");
                return;
            }

            try
            {
                LoadingIndicator.IsRunning = true;

                // 获取当前用户ID
                int currentUserId = App.CurrentUser?.UserId ?? 1; // 从当前登录用户获取，如果未登录则使用默认值

                _logger?.LogInformation("开始保存餐食记录 - 用户ID: {UserId}, 食物: {FoodName}, 标签: {FoodTag}", 
                    currentUserId, _currentFoodName, _currentFoodTag);

                // 1. 先检查并创建食物记录
                int foodId = await GetOrCreateFoodAsync(_currentFoodName);
                _logger?.LogInformation("获取到食物ID: {FoodId}", foodId);

                // 2. 如果有标签，检查并创建标签记录
                int? tagId = null;
                if (!string.IsNullOrEmpty(_currentFoodTag))
                {
                    tagId = await GetOrCreateTagAsync(_currentFoodTag);
                    _logger?.LogInformation("获取到标签ID: {TagId}", tagId);
                }

                // 3. 创建餐食记录
                var currentDate = DateOnly.FromDateTime(DateTime.Now);
                var currentTime = TimeOnly.FromDateTime(DateTime.Now);
                var mealType = GetMealTypeByTime(currentTime);
                _logger?.LogInformation("当前餐食类型: {MealType}, 日期: {Date}, 时间: {Time}", mealType, currentDate, currentTime);

                var meal = await _mealService.AddMealAsync(currentUserId, mealType, currentDate, currentTime);
                _logger?.LogInformation("创建餐食记录成功: MealId={MealId}", meal.MealId);

                // 4. 添加食物到餐食
                await _mealService.AddFoodToMealAsync(meal.MealId, foodId, _ossImagePath);
                _logger?.LogInformation("添加食物到餐食成功: MealId={MealId}, FoodId={FoodId}", meal.MealId, foodId);

                // 5. 如果有标签，添加标签到餐食
                if (tagId.HasValue)
                {
                    await _mealService.AddTagToMealFoodAsync(meal.MealId, foodId, tagId.Value);
                    _logger?.LogInformation("添加标签到餐食成功: MealId={MealId}, FoodId={FoodId}, TagId={TagId}", meal.MealId, foodId, tagId);
                }

                // 6. 显示成功消息
                var successMessage = $"餐食记录已保存成功！\n\n" +
                                   $"🍽️ 餐食类型: {mealType}\n" +
                                   $"🥘 食物名称: {_currentFoodName}\n" +
                                   $"🏷️ 食物标签: {(string.IsNullOrEmpty(_currentFoodTag) ? "无" : _currentFoodTag)}\n" +
                                   $"📅 记录时间: {currentDate} {currentTime:HH:mm}";

                await DisplayAlert("保存成功", successMessage, "确定");
                
                // 重置界面
                ResetInterface();
                
                // 刷新历史记录
                await LoadMealHistoryAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "保存餐食记录失败");
                
                // 提供更详细的错误信息
                var errorMessage = ex.InnerException != null 
                    ? $"{ex.Message}\n详细信息: {ex.InnerException.Message}" 
                    : ex.Message;
                    
                await DisplayAlert("保存失败", $"保存时出错:\n{errorMessage}", "确定");
            }
            finally
            {
                LoadingIndicator.IsRunning = false;
            }
        }

        #region 用餐历史相关方法

        /// <summary>
        /// 加载用餐历史
        /// </summary>
        private async Task LoadMealHistoryAsync()
        {
            try
            {
                _logger?.LogInformation("开始加载用餐历史");
                
                // 获取当前用户ID
                int currentUserId = App.CurrentUser?.UserId ?? 1; // 从当前登录用户获取，如果未登录则使用默认值

                // 获取最近7天的用餐记录
                var endDate = DateOnly.FromDateTime(DateTime.Today);
                var startDate = endDate.AddDays(-7);

                var mealHistory = await _mealService.GetUserMealsByDateRangeAsync(currentUserId, startDate, endDate);
                _logger?.LogInformation("从数据库获取到用户 {UserId} 的 {Count} 条餐食记录", currentUserId, mealHistory.Count);
                
                // 按时间倒序排列（最新的在上面）
                var sortedHistory = mealHistory
                    .OrderByDescending(m => m.MealDate)
                    .ThenByDescending(m => m.MealTime)
                    .Take(10) // 只显示最近10条记录
                    .ToList();

                _logger?.LogInformation("排序后准备处理 {Count} 条记录", sortedHistory.Count);

                // 为每个餐食记录的图片生成OSS访问URL
                foreach (var meal in sortedHistory)
                {
                    _logger?.LogInformation("处理餐食记录 MealId={MealId}, 包含 {FoodCount} 个食物", 
                        meal.MealId, meal.MealFoodImages?.Count ?? 0);
                    
                    if (meal.MealFoodImages != null)
                    {
                        var updatedMealFoodImages = new List<DataAccessLib.Models.MealFoodImage>();
                        
                        foreach (var mealFoodImage in meal.MealFoodImages)
                        {
                            _logger?.LogInformation("处理食物图片: MealId={MealId}, FoodId={FoodId}, ImagePath={ImagePath}", 
                                mealFoodImage.MealId, mealFoodImage.FoodId, mealFoodImage.ImagePath ?? "null");
                            
                            if (!string.IsNullOrEmpty(mealFoodImage.ImagePath))
                            {
                                try
                                {
                                    // 使用OssService的GetFileUrl方法生成访问URL
                                    var imageUrl = _ossService.GetFileUrl(mealFoodImage.ImagePath, DateTime.Now.AddHours(24));
                                    _logger?.LogInformation("生成OSS URL成功: {ImagePath} -> {ImageUrl}", 
                                        mealFoodImage.ImagePath, imageUrl);
                                    
                                    // 创建一个扩展对象来包含ImageUrl
                                    var extendedMealFoodImage = new MealFoodImageViewModel
                                    {
                                        MealId = mealFoodImage.MealId,
                                        FoodId = mealFoodImage.FoodId,
                                        ImagePath = mealFoodImage.ImagePath,
                                        ImageUrl = imageUrl,
                                        Food = mealFoodImage.Food
                                    };
                                    
                                    updatedMealFoodImages.Add(extendedMealFoodImage);
                                }
                                catch (Exception ex)
                                {
                                    _logger?.LogWarning(ex, "生成OSS图片URL失败: {ImagePath}", mealFoodImage.ImagePath);
                                    
                                    // 如果生成URL失败，使用默认的OSS直接访问URL
                                    var defaultUrl = _mealService.GetImageUrl(mealFoodImage.ImagePath);
                                    _logger?.LogInformation("使用默认URL: {ImagePath} -> {DefaultUrl}", 
                                        mealFoodImage.ImagePath, defaultUrl);
                                    
                                    var extendedMealFoodImage = new MealFoodImageViewModel
                                    {
                                        MealId = mealFoodImage.MealId,
                                        FoodId = mealFoodImage.FoodId,
                                        ImagePath = mealFoodImage.ImagePath,
                                        ImageUrl = defaultUrl,
                                        Food = mealFoodImage.Food
                                    };
                                    
                                    updatedMealFoodImages.Add(extendedMealFoodImage);
                                }
                            }
                            else
                            {
                                _logger?.LogInformation("图片路径为空，跳过URL生成: MealId={MealId}, FoodId={FoodId}", 
                                    mealFoodImage.MealId, mealFoodImage.FoodId);
                                
                                // 即使没有图片也要添加到列表中
                                var extendedMealFoodImage = new MealFoodImageViewModel
                                {
                                    MealId = mealFoodImage.MealId,
                                    FoodId = mealFoodImage.FoodId,
                                    ImagePath = mealFoodImage.ImagePath,
                                    ImageUrl = string.Empty,
                                    Food = mealFoodImage.Food
                                };
                                
                                updatedMealFoodImages.Add(extendedMealFoodImage);
                            }
                        }
                        
                        // 替换整个集合
                        meal.MealFoodImages = updatedMealFoodImages;
                        _logger?.LogInformation("餐食 MealId={MealId} 的图片处理完成，共 {Count} 个食物", 
                            meal.MealId, updatedMealFoodImages.Count);
                    }
                }

                _logger?.LogInformation("设置CollectionView数据源，共 {Count} 条记录", sortedHistory.Count);
                MealHistoryCollectionView.ItemsSource = sortedHistory;
                
                // 控制空状态显示
                EmptyStateLayout.IsVisible = !sortedHistory.Any();
                ViewMoreButton.IsVisible = sortedHistory.Any();

                _logger?.LogInformation("加载用餐历史成功，共 {Count} 条记录", sortedHistory.Count);
                
                // 额外调试：检查第一条记录的详细信息
                if (sortedHistory.Any())
                {
                    var firstMeal = sortedHistory.First();
                    _logger?.LogInformation("第一条餐食记录详情: MealId={MealId}, MealType={MealType}, FoodCount={FoodCount}", 
                        firstMeal.MealId, firstMeal.MealType, firstMeal.MealFoodImages?.Count ?? 0);
                    
                    if (firstMeal.MealFoodImages?.Any() == true)
                    {
                        var firstFood = firstMeal.MealFoodImages.First();
                        if (firstFood is MealFoodImageViewModel viewModel)
                        {
                            _logger?.LogInformation("第一个食物详情: FoodName={FoodName}, ImagePath={ImagePath}, ImageUrl={ImageUrl}", 
                                firstFood.Food?.Name ?? "null", 
                                firstFood.ImagePath ?? "null", 
                                viewModel.ImageUrl ?? "null");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "加载用餐历史失败");
                EmptyStateLayout.IsVisible = true;
                ViewMoreButton.IsVisible = false;
            }
        }

        /// <summary>
        /// 刷新历史记录
        /// </summary>
        private async void OnRefreshHistoryClicked(object sender, EventArgs e)
        {
            try
            {
                RefreshHistoryButton.IsEnabled = false;
                RefreshHistoryButton.Text = "⏳";
                
                await LoadMealHistoryAsync();
                
                // 显示刷新成功提示
                await DisplayAlert("刷新成功", "用餐历史已更新", "确定");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "刷新历史记录失败");
                await DisplayAlert("刷新失败", "无法获取最新数据，请稍后再试", "确定");
            }
            finally
            {
                RefreshHistoryButton.IsEnabled = true;
                RefreshHistoryButton.Text = "🔄";
            }
        }

        /// <summary>
        /// 查看更多历史记录
        /// </summary>
        private async void OnViewMoreHistoryClicked(object sender, EventArgs e)
        {
            try
            {
                // 这里可以导航到一个专门的历史记录页面
                // 或者加载更多数据到当前页面
                
                await DisplayAlert("功能提示", "查看更多历史记录功能待开发\n\n当前显示最近10条记录", "确定");
                
                // TODO: 实现导航到历史记录详情页面
                // await Shell.Current.GoToAsync("//history");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "查看更多历史记录失败");
            }
        }

        /// <summary>
        /// 获取餐食类型对应的图标
        /// </summary>
        private string GetMealTypeIcon(string mealType)
        {
            return mealType switch
            {
                "早饭" => "🌅",
                "午饭" => "☀️",
                "晚饭" => "🌙",
                "零食" => "🍪",
                _ => "🍽️"
            };
        }

        /// <summary>
        /// 格式化餐食时间显示
        /// </summary>
        private string FormatMealTime(DateOnly date, TimeOnly time)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var yesterday = today.AddDays(-1);

            string dateStr = date switch
            {
                var d when d == today => "今天",
                var d when d == yesterday => "昨天",
                _ => date.ToString("MM/dd")
            };

            return $"{dateStr} {time:HH:mm}";
        }

        #endregion

        /// <summary>
        /// 取消操作
        /// </summary>
        private void OnCancelClicked(object sender, EventArgs e)
        {
            ResetInterface();
        }

        /// <summary>
        /// 重置界面
        /// </summary>
        private void ResetInterface()
        {
            ResultFrame.IsVisible = false;
            PreviewImage.IsVisible = false;
            
            _currentFoodName = "";
            _currentFoodTag = "";
            
            CleanupLocalImage();
        }

        /// <summary>
        /// 根据时间判断餐食类型
        /// </summary>
        private string GetMealTypeByTime(TimeOnly time)
        {
            var hour = time.Hour;
            return hour switch
            {
                >= 5 and < 12 => "早饭",
                >= 12 and < 15 => "午饭",
                >= 15 and < 18 => "零食",
                >= 18 and < 24 => "晚饭",
            };
        }

        /// <summary>
        /// 上传图片到OSS
        /// </summary>
        private async Task UploadImageToOssAsync(string localImagePath, string fileName)
        {
            try
            {
                // 生成OSS对象名称
                var fileExtension = Path.GetExtension(fileName);
                var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                var guid = Guid.NewGuid().ToString("N")[..8];
                var ossObjectName = $"food-photos/{DateTime.Now:yyyy/MM/dd}/{timestamp}_{guid}{fileExtension}";

                _logger?.LogInformation("开始上传图片到OSS: {LocalPath} -> {OssPath}", localImagePath, ossObjectName);

                // 在后台线程执行上传
                await Task.Run(() =>
                {
                    _ossService.UploadFile(ossObjectName, localImagePath);
                });

                _ossImagePath = ossObjectName;
                _logger?.LogInformation("图片上传成功: {OssPath}", ossObjectName);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "上传图片到OSS失败: {LocalPath}", localImagePath);
                throw;
            }
        }

        /// <summary>
        /// 清理本地缓存图片
        /// </summary>
        private void CleanupLocalImage()
        {
            try
            {
                if (!string.IsNullOrEmpty(_capturedImagePath) && File.Exists(_capturedImagePath))
                {
                    File.Delete(_capturedImagePath);
                    _logger?.LogInformation("已清理本地缓存图片: {Path}", _capturedImagePath);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "清理本地缓存图片失败: {Path}", _capturedImagePath);
            }
            finally
            {
                _capturedImagePath = null;
                _ossImagePath = null;
                _capturedImageData = null;
            }
        }

        /// <summary>
        /// 检查和请求相册权限
        /// </summary>
        private async Task<bool> CheckAndRequestGalleryPermissions()
        {
            try
            {
                var mediaStatus = await Permissions.CheckStatusAsync<Permissions.Media>();
                if (mediaStatus != PermissionStatus.Granted)
                {
                    mediaStatus = await Permissions.RequestAsync<Permissions.Media>();
                }

                // 对于较低版本的Android，可能需要检查Photos权限
                var photosStatus = await Permissions.CheckStatusAsync<Permissions.Photos>();
                if (photosStatus != PermissionStatus.Granted)
                {
                    photosStatus = await Permissions.RequestAsync<Permissions.Photos>();
                }

                return mediaStatus == PermissionStatus.Granted || photosStatus == PermissionStatus.Granted;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "相册权限检查失败");
                return false;
            }
        }

        private async Task<bool> CheckAndRequestPermissions()
        {
            try
            {
                var cameraStatus = await Permissions.CheckStatusAsync<Permissions.Camera>();
                if (cameraStatus != PermissionStatus.Granted)
                {
                    cameraStatus = await Permissions.RequestAsync<Permissions.Camera>();
                }
                return cameraStatus == PermissionStatus.Granted;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "权限检查失败");
                return false;
            }
        }

        private async Task<byte[]> ReadFully(Stream input)
        {
            using var ms = new MemoryStream();
            await input.CopyToAsync(ms);
            return ms.ToArray();
        }

        /// <summary>
        /// 扩展的MealFoodImage视图模型，包含ImageUrl属性
        /// </summary>
        public class MealFoodImageViewModel : DataAccessLib.Models.MealFoodImage
        {
            public string ImageUrl { get; set; } = string.Empty;
        }

        #region 图片预览相关方法

        /// <summary>
        /// 缩略图点击事件
        /// </summary>
        private async void OnThumbnailTapped(object sender, EventArgs e)
        {
            try
            {
                if (sender is Frame frame && frame.BindingContext is MealFoodImageViewModel viewModel)
                {
                    if (!string.IsNullOrEmpty(viewModel.ImageUrl))
                    {
                        await ShowImagePreviewAsync(viewModel.ImageUrl, viewModel.Food?.Name ?? "食物图片");
                    }
                    else
                    {
                        await DisplayAlert("提示", "该食物暂无图片", "确定");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "显示图片预览失败");
                await DisplayAlert("错误", "无法显示图片预览", "确定");
            }
        }

        /// <summary>
        /// 显示图片预览
        /// </summary>
        private async Task ShowImagePreviewAsync(string imageUrl, string foodName)
        {
            try
            {
                _currentPreviewImageUrl = imageUrl;
                _currentPreviewFoodName = foodName;
                
                // 设置标题和图片
                PreviewTitle.Text = foodName;
                PreviewInfo.Text = "正在加载图片...";
                
                // 加载图片
                PopupPreviewImage.Source = new UriImageSource
                {
                    Uri = new Uri(imageUrl),
                    CachingEnabled = true,
                    CacheValidity = TimeSpan.FromDays(1)
                };
                
                // 显示弹窗
                ImagePreviewOverlay.IsVisible = true;
                
                // 添加渐入动画
                ImagePreviewOverlay.Opacity = 0;
                await ImagePreviewOverlay.FadeTo(1, 300);
                
                PreviewInfo.Text = "点击空白处关闭";
                
                _logger?.LogInformation("显示图片预览: {FoodName} - {ImageUrl}", foodName, imageUrl);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "加载预览图片失败: {ImageUrl}", imageUrl);
                PreviewInfo.Text = "图片加载失败";
            }
        }

        /// <summary>
        /// 关闭预览弹窗
        /// </summary>
        private async void OnClosePreviewClicked(object sender, EventArgs e)
        {
            await CloseImagePreviewAsync();
        }

        /// <summary>
        /// 点击遮罩层关闭预览
        /// </summary>
        private async void OnOverlayTapped(object sender, EventArgs e)
        {
            await CloseImagePreviewAsync();
        }

        /// <summary>
        /// 关闭图片预览
        /// </summary>
        private async Task CloseImagePreviewAsync()
        {
            try
            {
                // 添加渐出动画
                await ImagePreviewOverlay.FadeTo(0, 200);
                ImagePreviewOverlay.IsVisible = false;
                
                // 清理资源
                PopupPreviewImage.Source = null;
                _currentPreviewImageUrl = "";
                _currentPreviewFoodName = "";
                
                _logger?.LogInformation("关闭图片预览");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "关闭图片预览失败");
            }
        }

        /// <summary>
        /// 查看原图
        /// </summary>
        private async void OnViewOriginalClicked(object sender, EventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(_currentPreviewImageUrl))
                {
                    // 在浏览器中打开原图
                    await Browser.OpenAsync(_currentPreviewImageUrl, BrowserLaunchMode.SystemPreferred);
                    _logger?.LogInformation("在浏览器中打开原图: {ImageUrl}", _currentPreviewImageUrl);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "打开原图失败: {ImageUrl}", _currentPreviewImageUrl);
                await DisplayAlert("错误", "无法打开原图", "确定");
            }
        }

        /// <summary>
        /// 处理返回键或手势（可选）
        /// </summary>
        protected override bool OnBackButtonPressed()
        {
            if (ImagePreviewOverlay.IsVisible)
            {
                _ = CloseImagePreviewAsync();
                return true; // 拦截返回键事件
            }
            return base.OnBackButtonPressed();
        }

        #endregion

        private async void OnSettingsClicked(object sender, EventArgs e)
        {
            try
            {
                await Shell.Current.GoToAsync(nameof(SettingsPage));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "跳转到设置页面失败");
                await DisplayAlert("错误", "无法打开设置页面", "确定");
            }
        }
        #region 手动输入相关方法

        /// <summary>
        /// 手动输入按钮点击事件
        /// </summary>
        private async void OnManualInputClicked(object sender, EventArgs e)
        {
            try
            {
                LoadingIndicator.IsRunning = true;
                
                // 重置选择状态
                ResetManualInputState();
                
                // 先显示手动输入弹窗
                await ShowManualInputOverlayAsync();
                
                // 后台异步加载用户历史数据
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await LoadUserHistoryDataAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "后台加载用户历史数据失败");
                        // 在UI线程上显示错误信息
                        MainThread.BeginInvokeOnMainThread(async () =>
                        {
                            await DisplayAlert("提示", "加载历史数据失败，但您仍可以手动输入", "确定");
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "打开手动输入界面失败");
                await DisplayAlert("错误", $"无法打开手动输入界面: {ex.Message}", "确定");
            }
            finally
            {
                LoadingIndicator.IsRunning = false;
            }
        }

        /// <summary>
        /// 加载用户历史数据
        /// </summary>
        private async Task LoadUserHistoryDataAsync()
        {
            try
            {
                int currentUserId = 1; // 临时硬编码
                
                _logger?.LogInformation("开始加载用户历史数据，用户ID: {UserId}", currentUserId);
                
                // 创建新的临时列表来避免在UI线程上的竞争条件
                List<DataAccessLib.Models.Food> tempFoods = new List<DataAccessLib.Models.Food>();
                List<DataAccessLib.Models.Tag> tempTags = new List<DataAccessLib.Models.Tag>();
                
                try
                {
                    // 串行加载历史食物
                    tempFoods = await _foodService.GetUserHistoryFoodsAsync(currentUserId, 15);
                    _logger?.LogInformation("加载历史食物成功: {Count} 个", tempFoods.Count);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "加载历史食物失败，使用空列表");
                    tempFoods = new List<DataAccessLib.Models.Food>();
                }
                
                // 添加小延迟确保数据库连接释放
                await Task.Delay(100);
                
                try
                {
                    // 串行加载历史标签
                    tempTags = await _tagService.GetUserHistoryTagsAsync(currentUserId, 20);
                    _logger?.LogInformation("加载历史标签成功: {Count} 个", tempTags.Count);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "加载历史标签失败，使用空列表");
                    tempTags = new List<DataAccessLib.Models.Tag>();
                }
                
                // 在UI线程上更新界面
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        _userHistoryFoods = tempFoods;
                        _userHistoryTags = tempTags;
                        
                        // 更新UI
                        HistoryFoodCollectionView.ItemsSource = _userHistoryFoods;
                        HistoryTagCollectionView.ItemsSource = _userHistoryTags;
                        
                        _logger?.LogInformation("UI更新完成: {FoodCount} 个食物, {TagCount} 个标签", 
                            _userHistoryFoods.Count, _userHistoryTags.Count);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "更新UI失败");
                    }
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "加载用户历史数据失败");
                
                // 在UI线程上设置空列表避免UI报错
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _userHistoryFoods = new List<DataAccessLib.Models.Food>();
                    _userHistoryTags = new List<DataAccessLib.Models.Tag>();
                    
                    HistoryFoodCollectionView.ItemsSource = _userHistoryFoods;
                    HistoryTagCollectionView.ItemsSource = _userHistoryTags;
                });
                
                throw new Exception($"加载历史数据失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 重置手动输入状态
        /// </summary>
        private void ResetManualInputState()
        {
            _selectedFoodName = "";
            _selectedTags.Clear();
            
            ManualFoodNameEntry.Text = "";
            ManualTagEntry.Text = "";
            
            UpdateSelectedTagsDisplay();
        }

        /// <summary>
        /// 显示手动输入弹窗
        /// </summary>
        private async Task ShowManualInputOverlayAsync()
        {
            ManualInputOverlay.IsVisible = true;
            ManualInputOverlay.Opacity = 0;
            await ManualInputOverlay.FadeTo(1, 300);
        }

        /// <summary>
        /// 关闭手动输入弹窗
        /// </summary>
        private async void OnCloseManualInputClicked(object sender, EventArgs e)
        {
            await CloseManualInputOverlayAsync();
        }

        /// <summary>
        /// 点击遮罩层关闭手动输入弹窗
        /// </summary>
        private async void OnManualInputOverlayTapped(object sender, EventArgs e)
        {
            await CloseManualInputOverlayAsync();
        }

        /// <summary>
        /// 关闭手动输入弹窗
        /// </summary>
        private async Task CloseManualInputOverlayAsync()
        {
            try
            {
                await ManualInputOverlay.FadeTo(0, 200);
                ManualInputOverlay.IsVisible = false;
                
                _logger?.LogInformation("关闭手动输入弹窗");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "关闭手动输入弹窗失败");
            }
        }

        /// <summary>
        /// 历史食物点击事件
        /// </summary>
        private void OnHistoryFoodTapped(object sender, EventArgs e)
        {
            try
            {
                if (sender is Border border && border.BindingContext is DataAccessLib.Models.Food food)
                {
                    _selectedFoodName = food.Name;
                    ManualFoodNameEntry.Text = food.Name;
                    
                    // 更新视觉状态
                    UpdateFoodSelectionVisual();
                    
                    _logger?.LogInformation("选择历史食物: {FoodName}", food.Name);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "选择历史食物失败");
            }
        }

        /// <summary>
        /// 历史标签点击事件
        /// </summary>
        private void OnHistoryTagTapped(object sender, EventArgs e)
        {
            try
            {
                if (sender is Border border && border.BindingContext is DataAccessLib.Models.Tag tag)
                {
                    // 切换标签选择状态
                    if (_selectedTags.Any(t => t.TagId == tag.TagId))
                    {
                        // 取消选择
                        _selectedTags.RemoveAll(t => t.TagId == tag.TagId);
                        border.BackgroundColor = Color.FromArgb("#E3F2FD");
                        border.Stroke = Color.FromArgb("#2196F3");
                    }
                    else
                    {
                        // 选择标签
                        _selectedTags.Add(tag);
                        border.BackgroundColor = Color.FromArgb("#4CAF50");
                        border.Stroke = Color.FromArgb("#4CAF50");
                    }
                    
                    UpdateSelectedTagsDisplay();
                    
                    _logger?.LogInformation("切换标签选择: {TagName}, 当前选中: {Count} 个", 
                        tag.TagName, _selectedTags.Count);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "选择历史标签失败");
            }
        }

        /// <summary>
        /// 添加新标签按钮点击事件
        /// </summary>
        private void OnAddNewTagClicked(object sender, EventArgs e)
        {
            try
            {
                var newTagName = ManualTagEntry.Text?.Trim();
                if (string.IsNullOrEmpty(newTagName))
                {
                    DisplayAlert("提示", "请输入标签名称", "确定");
                    return;
                }

                // 检查是否已经选择过
                if (_selectedTags.Any(t => t.TagName.Equals(newTagName, StringComparison.OrdinalIgnoreCase)))
                {
                    DisplayAlert("提示", "该标签已选择", "确定");
                    return;
                }

                // 创建临时标签对象（ID为0表示新标签）
                var newTag = new DataAccessLib.Models.Tag
                {
                    TagId = 0,
                    TagName = newTagName
                };

                _selectedTags.Add(newTag);
                ManualTagEntry.Text = "";
                
                UpdateSelectedTagsDisplay();
                
                _logger?.LogInformation("添加新标签: {TagName}", newTagName);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "添加新标签失败");
            }
        }

        /// <summary>
        /// 移除选中标签按钮点击事件
        /// </summary>
        private void OnRemoveSelectedTagClicked(object sender, EventArgs e)
        {
            try
            {
                if (sender is Button button && button.BindingContext is DataAccessLib.Models.Tag tag)
                {
                    _selectedTags.RemoveAll(t => t.TagId == tag.TagId && t.TagName == tag.TagName);
                    UpdateSelectedTagsDisplay();
                    
                    // 更新历史标签的视觉状态
                    UpdateTagSelectionVisual();
                    
                    _logger?.LogInformation("移除选中标签: {TagName}", tag.TagName);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "移除选中标签失败");
            }
        }

        /// <summary>
        /// 保存手动输入按钮点击事件
        /// </summary>
        private async void OnSaveManualInputClicked(object sender, EventArgs e)
        {
            try
            {
                // 获取食物名称
                var foodName = !string.IsNullOrEmpty(_selectedFoodName) 
                    ? _selectedFoodName 
                    : ManualFoodNameEntry.Text?.Trim();

                if (string.IsNullOrEmpty(foodName))
                {
                    await DisplayAlert("提示", "请选择或输入食物名称", "确定");
                    return;
                }

                LoadingIndicator.IsRunning = true;

                // 获取当前用户ID
                int currentUserId = 1; // 临时硬编码

                _logger?.LogInformation("开始保存手动输入的餐食记录 - 食物: {FoodName}, 标签数量: {TagCount}", 
                    foodName, _selectedTags.Count);

                // 串行执行所有数据库操作，避免并发问题
                await ExecuteDatabaseOperationsAsync(currentUserId, foodName);

                // 显示成功消息
                var tagNames = _selectedTags.Any() 
                    ? string.Join(", ", _selectedTags.Select(t => t.TagName))
                    : "无";

                var currentDate = DateOnly.FromDateTime(DateTime.Now);
                var currentTime = TimeOnly.FromDateTime(DateTime.Now);
                var mealType = GetMealTypeByTime(currentTime);

                var successMessage = $"餐食记录已保存成功！\n\n" +
                                   $"🍽️ 餐食类型: {mealType}\n" +
                                   $"🥘 食物名称: {foodName}\n" +
                                   $"🏷️ 食物标签: {tagNames}\n" +
                                   $"📅 记录时间: {currentDate} {currentTime:HH:mm}";

                await DisplayAlert("保存成功", successMessage, "确定");

                // 关闭弹窗并刷新历史记录
                await CloseManualInputOverlayAsync();
                await LoadMealHistoryAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "保存手动输入的餐食记录失败");
                
                var errorMessage = ex.InnerException != null 
                    ? $"{ex.Message}\n详细信息: {ex.InnerException.Message}" 
                    : ex.Message;
                    
                await DisplayAlert("保存失败", $"保存时出错:\n{errorMessage}", "确定");
            }
            finally
            {
                LoadingIndicator.IsRunning = false;
            }
        }

        /// <summary>
        /// 串行执行数据库操作
        /// </summary>
        private async Task ExecuteDatabaseOperationsAsync(int currentUserId, string foodName)
        {
            try
            {
                // 1. 获取或创建食物记录
                int foodId = await GetOrCreateFoodAsync(foodName);
                _logger?.LogInformation("获取到食物ID: {FoodId}", foodId);
                
                // 添加小延迟确保数据库操作完成
                await Task.Delay(50);

                // 2. 创建餐食记录
                var currentDate = DateOnly.FromDateTime(DateTime.Now);
                var currentTime = TimeOnly.FromDateTime(DateTime.Now);
                var mealType = GetMealTypeByTime(currentTime);

                var meal = await _mealService.AddMealAsync(currentUserId, mealType, currentDate, currentTime);
                _logger?.LogInformation("创建餐食记录成功: MealId={MealId}", meal.MealId);
                
                await Task.Delay(50);

                // 3. 添加食物到餐食（不带图片）
                await _mealService.AddFoodToMealAsync(meal.MealId, foodId, null);
                _logger?.LogInformation("添加食物到餐食成功: MealId={MealId}, FoodId={FoodId}", meal.MealId, foodId);
                
                await Task.Delay(50);

                // 4. 处理标签（串行处理每个标签）
                foreach (var selectedTag in _selectedTags)
                {
                    try
                    {
                        int tagId;
                        if (selectedTag.TagId == 0) // 新标签
                        {
                            tagId = await GetOrCreateTagAsync(selectedTag.TagName);
                            await Task.Delay(50); // 确保标签创建完成
                        }
                        else // 已有标签
                        {
                            tagId = selectedTag.TagId;
                        }

                        await _mealService.AddTagToMealFoodAsync(meal.MealId, foodId, tagId);
                        _logger?.LogInformation("添加标签到餐食成功: MealId={MealId}, FoodId={FoodId}, TagId={TagId}", 
                            meal.MealId, foodId, tagId);
                            
                        await Task.Delay(50); // 每个标签操作之间添加延迟
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "添加标签失败: {TagName}", selectedTag.TagName);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "数据库操作失败");
                throw;
            }
        }

        /// <summary>
        /// 更新食物选择的视觉效果
        /// </summary>
        private void UpdateFoodSelectionVisual()
        {
            // 这里可以添加视觉反馈，比如高亮选中的食物
            // 由于CollectionView的限制，暂时通过文本框显示选择结果
        }

        /// <summary>
        /// 更新标签选择的视觉效果
        /// </summary>
        private void UpdateTagSelectionVisual()
        {
            // 重置所有历史标签的视觉状态
            // 注意：这里需要遍历UI元素，实际实现可能需要更复杂的逻辑
        }

        /// <summary>
        /// 更新选中标签的显示
        /// </summary>
        private void UpdateSelectedTagsDisplay()
        {
            SelectedTagsCollectionView.ItemsSource = _selectedTags.ToList();
            SelectedTagsLayout.IsVisible = _selectedTags.Any();
        }

        #endregion
    }
}