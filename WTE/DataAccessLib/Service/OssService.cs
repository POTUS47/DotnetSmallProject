using Aliyun.OSS;
using Aliyun.OSS.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;


namespace DataAccessLib.Services
{
    public class OssService
    {
        private readonly OssClient _ossClient;

        public OssService()
        {
            var endpoint = "https://oss-cn-hangzhou.aliyuncs.com";
            var accessKeyId = "LTAI5tSfy7bGaRgUsJEFpSN5";
            var accessKeySecret = "Ia9tJxMvzNvJJ1TtDDH7Q6xC0hb5tl";
            var bucketName = "wte";
            var region = "cn-hangzhou";
            // 创建ClientConfiguration实例，按照您的需要修改默认参数。
            var conf = new ClientConfiguration();

            // 设置v4签名。
            conf.SignatureVersion = SignatureVersion.V4;

            _ossClient = new OssClient(endpoint, accessKeyId, accessKeySecret, conf);
            _ossClient.SetRegion(region);
            try
            {
                var bucket = _ossClient.CreateBucket(bucketName);
                Console.WriteLine("Create bucket succeeded, {0} ", bucket.Name);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Create bucket failed, {0}", ex.Message);
            }
            
        }

        /// <summary>
        /// 上传本地文件到OSS
        /// </summary>
        public void UploadFile(string objectName, string localFilename)
        {
            // 填写Object完整路径，完整路径中不能包含Bucket名称，例如exampledir/exampleobject.txt。
            // var objectName = "exampledir/exampleobject.txt";
            // 填写本地文件完整路径，例如D:\\localpath\\examplefile.txt。如果未指定本地路径，则默认从示例程序所属项目对应本地路径中上传文件。
            // var localFilename = "D:\\localpath\\examplefile.txt";
            var bucketName = "wte";

            try
            {
                // 上传文件。
                var result = _ossClient.PutObject(bucketName, objectName, localFilename);
                Console.WriteLine("Put object succeeded, ETag: {0} ", result.ETag);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Put object failed, {0}", ex.Message);
            }

        }

        /// <summary>
        /// 下载OSS文件到本地
        /// </summary>
        public void DownloadFile(string objectName, string downloadFilename)
        {
            // 填写Object完整路径，完整路径中不能包含Bucket名称，例如exampledir/exampleobject.txt。
            // var objectName = "exampledir/exampleobject.txt";
            // 下载Object到本地文件examplefile.txt，并保存到指定的本地路径中（D:\\localpath）。如果指定的本地文件存在会覆盖，不存在则新建。
            // 如果未指定本地路径，则下载后的文件默认保存到示例程序所属项目对应本地路径中。
            // var downloadFilename = "D:\\localpath\\examplefile.txt";
            var bucketName = "wte";

            try
            {
                // 下载文件。
                var result = _ossClient.GetObject(bucketName, objectName);
                using (var requestStream = result.Content)
                {
                    using (var fs = File.Open(downloadFilename, FileMode.OpenOrCreate))
                    {
                        int length = 4 * 1024;
                        var buf = new byte[length];
                        do
                        {
                            length = requestStream.Read(buf, 0, length);
                            fs.Write(buf, 0, length);
                        } while (length != 0);
                    }
                }
                Console.WriteLine("Get object succeeded");
            }
            catch (OssException ex)
            {
                Console.WriteLine("Failed with error code: {0}; Error info: {1}. \nRequestID:{2}\tHostID:{3}",
                    ex.ErrorCode, ex.Message, ex.RequestId, ex.HostId);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed with error info: {0}", ex.Message);
            }
        }

        /// <summary>
        /// 创建存储空间（Bucket）
        /// </summary>
        public void CreateBucket(string bucketName)
        {
            try
            {
                _ossClient.CreateBucket(bucketName);
                Console.WriteLine($"创建Bucket成功: {bucketName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"创建Bucket失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 删除OSS中的文件
        /// </summary>
        public void DeleteFile(string objectName)
        {
            var bucketName = "wte";

            try
            {
                _ossClient.DeleteObject(bucketName, objectName);
                Console.WriteLine($"Delete object succeeded: {objectName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Delete object failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 检查文件是否存在
        /// </summary>
        public bool FileExists(string objectName)
        {
            var bucketName = "wte";

            try
            {
                var result = _ossClient.DoesObjectExist(bucketName, objectName);
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Check object existence failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取文件访问URL
        /// </summary>
        public string GetFileUrl(string objectName, DateTime? expiration = null)
        {
            var bucketName = "wte";

            try
            {
                // 如果没有指定过期时间，默认1小时
                var exp = expiration ?? DateTime.Now.AddHours(1);
                var uri = _ossClient.GeneratePresignedUri(bucketName, objectName, exp);
                return uri.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Generate presigned URL failed: {ex.Message}");
                throw;
            }
        }
    }
}