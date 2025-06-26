using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using DataAccessLib.Services;
using System.IO;
using Microsoft.Maui.Storage;
using System.ComponentModel;
using System.Linq;
using Microsoft.Maui.Controls;

namespace WTEMaui.Services
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct TagCount
    {
        public int TagId;
        [MarshalAs(UnmanagedType.LPStr)]
        public string TagName;
        public int Count;
    }

    public class TagStatisticsService
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("CalculateTagLib.dll", EntryPoint = "CalculateUserTagStatistics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int CalculateUserTagStatistics(
            int timeRange,
            [MarshalAs(UnmanagedType.LPStr)] string startDate,
            int userId,
            [In, Out] TagCount[] outTagCounts,
            ref int outSize
        );

        private static string? _dllPath;
        private static string DllPath
        {
            get
            {
                if (_dllPath == null)
                {
                    string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                    string[] possiblePaths = new[]
                    {
                        Path.Combine(baseDirectory, "native", "CalculateTagLib.dll"),
                        Path.Combine(baseDirectory, "CalculateTagLib.dll"),
                        Path.Combine(AppContext.BaseDirectory, "native", "CalculateTagLib.dll"),
                        Path.Combine(AppContext.BaseDirectory, "CalculateTagLib.dll")
                    };

                    foreach (var path in possiblePaths)
                    {
                        if (File.Exists(path))
                        {
                            _dllPath = path;
                            break;
                        }
                    }

                    if (_dllPath == null)
                    {
                        throw new FileNotFoundException($"找不到DLL文件，已尝试以下路径：{string.Join(", ", possiblePaths)}");
                    }
                }
                return _dllPath;
            }
        }
        private readonly TagService _tagService;
        private static IntPtr _dllHandle;

        public TagStatisticsService(TagService tagService)
        {
            _tagService = tagService;
            
            try
            {
                // 设置DLL搜索路径
                string dllDirectory = Path.GetDirectoryName(DllPath) ?? string.Empty;
                if (!string.IsNullOrEmpty(dllDirectory))
                {
                    if (!SetDllDirectory(dllDirectory))
                    {
                        throw new Exception($"设置DLL目录失败: {new Win32Exception(Marshal.GetLastWin32Error()).Message}");
                    }
                }

                // 手动加载DLL
                _dllHandle = LoadLibrary(DllPath);
                if (_dllHandle == IntPtr.Zero)
                {
                    throw new Exception($"加载DLL失败: {new Win32Exception(Marshal.GetLastWin32Error()).Message}");
                }

                System.Diagnostics.Debug.WriteLine($"成功加载DLL，路径: {DllPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"初始化DLL时出错: {ex}");
                throw;
            }
        }

        public class TagStatistic
        {
            public int TagId { get; set; }
            public string TagName { get; set; } = "";
            public int Count { get; set; }
            public double Percentage { get; set; }
        }

        public List<TagStatistic> GetUserTagStatistics(int userId, DateTime startDate, DateTime endDate)
        {
            try
            {
                // 从数据库获取标签统计
                var dbStats = _tagService.GetUserTagStatistics(userId, startDate, endDate);

                if (!dbStats.Any())
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await Application.Current.MainPage.DisplayAlert("提示", 
                            "数据库返回空记录", 
                            "确定");
                    });
                    return new List<TagStatistic>();
                }

                // 准备数据传递给C++ DLL
                const int MaxTags = 100;
                var tagCounts = new TagCount[MaxTags];
                var size = Math.Min(MaxTags, dbStats.Count);

                for (int i = 0; i < size; i++)
                {
                    var stat = dbStats[i];
                    tagCounts[i] = new TagCount
                    {
                        TagId = stat.TagId,
                        TagName = stat.TagName,
                        Count = stat.Count
                    };
                }

                // 调用C++ DLL进行处理
                var timeRange = (endDate - startDate).Days <= 7 ? 1 : 2;

                var result = CalculateUserTagStatistics(
                    timeRange,
                    startDate.ToString("yyyy-MM-dd"),
                    userId,
                    tagCounts,
                    ref size
                );

                if (result != 0)
                {
                    throw new Exception($"调用原生方法失败，错误代码：{result}");
                }

                // 处理结果
                var statistics = new List<TagStatistic>();
                int total = 0;

                for (int i = 0; i < size; i++)
                {
                    total += tagCounts[i].Count;
                }

                for (int i = 0; i < size; i++)
                {
                    var percentage = total > 0 ? (double)tagCounts[i].Count / total : 0;
                    statistics.Add(new TagStatistic
                    {
                        TagId = tagCounts[i].TagId,
                        TagName = tagCounts[i].TagName,
                        Count = tagCounts[i].Count,
                        Percentage = percentage
                    });
                }

                return statistics;
            }
            catch (Exception ex)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await Application.Current.MainPage.DisplayAlert("错误", 
                        $"获取标签统计失败: {ex.Message}\n\n{ex.StackTrace}", 
                        "确定");
                });
                throw;
            }
        }
    }
}