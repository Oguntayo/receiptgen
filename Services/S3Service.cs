using Amazon.S3;
using Amazon.S3.Transfer;
using Microsoft.Extensions.Configuration;

namespace ReceiptGen.Services
{
    public interface IS3Service
    {
        Task<string> UploadReceiptAsync(byte[] pdfContent, string fileName);
        string GetPreSignedUrl(string key);
    }

    public class S3Service : IS3Service
    {
        private readonly IConfiguration _configuration;
        private readonly IAmazonS3 _s3Client;

        public S3Service(IConfiguration configuration)
        {
            _configuration = configuration;
            var awsSettings = _configuration.GetSection("AWS");
            if (awsSettings.Exists())
            {
                File.AppendAllText("email_logs.txt", $"[{DateTime.Now}] S3 DEBUG: AWS Settings found. Bucket: {awsSettings["BucketName"]}{Environment.NewLine}");
            }
            else
            {
                File.AppendAllText("email_logs.txt", $"[{DateTime.Now}] S3 ERROR: AWS Settings NOT found in appsettings.json!{Environment.NewLine}");
            }
            _s3Client = new AmazonS3Client(
                awsSettings["AccessKey"],
                awsSettings["SecretKey"],
                Amazon.RegionEndpoint.GetBySystemName(awsSettings["Region"]));
        }

        public async Task<string> UploadReceiptAsync(byte[] pdfContent, string fileName)
        {
            try
            {
                var awsSettings = _configuration.GetSection("AWS");
                var bucketName = awsSettings["BucketName"];
                
                File.AppendAllText("email_logs.txt", $"[{DateTime.Now}] S3 DEBUG: Uploading {fileName} to bucket {bucketName}{Environment.NewLine}");

                using (var ms = new MemoryStream(pdfContent))
                {
                    var uploadRequest = new TransferUtilityUploadRequest
                    {
                        InputStream = ms,
                        Key = $"receipts/{fileName}",
                        BucketName = bucketName,
                        ContentType = "application/pdf"
                    };

                    var fileTransferUtility = new TransferUtility(_s3Client);
                    await fileTransferUtility.UploadAsync(uploadRequest);

                    var key = $"receipts/{fileName}";
                    File.AppendAllText("email_logs.txt", $"[{DateTime.Now}] S3 DEBUG: Upload successful. Key: {key}{Environment.NewLine}");
                    return key;
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText("email_logs.txt", $"[{DateTime.Now}] S3 ERROR: {ex.Message}{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}");
                throw;
            }
        }

        public string GetPreSignedUrl(string keyOrUrl)
        {
            var awsSettings = _configuration.GetSection("AWS");
            var bucketName = awsSettings["BucketName"];
            
            string key = keyOrUrl;
            
            // If it's a full URL, try to extract the key
            if (keyOrUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                var uri = new Uri(keyOrUrl);
                // For virtual-hosted style (bucket.s3.region.amazonaws.com/key)
                // path will be /key
                key = uri.AbsolutePath.TrimStart('/');
            }

            var request = new Amazon.S3.Model.GetPreSignedUrlRequest
            {
                BucketName = bucketName,
                Key = key,
                Expires = DateTime.UtcNow.AddHours(1) // Link valid for 1 hour
            };

            return _s3Client.GetPreSignedURL(request);
        }
    }
}
