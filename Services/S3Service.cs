using Amazon.S3;
using Amazon.S3.Transfer;
using Microsoft.Extensions.Configuration;

namespace ReceiptGen.Services
{
    public interface IS3Service
    {
        Task<string> UploadReceiptAsync(byte[] pdfContent, string fileName);
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

                    var url = $"https://{bucketName}.s3.{awsSettings["Region"]}.amazonaws.com/receipts/{fileName}";
                    File.AppendAllText("email_logs.txt", $"[{DateTime.Now}] S3 DEBUG: Upload successful. URL: {url}{Environment.NewLine}");
                    return url;
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText("email_logs.txt", $"[{DateTime.Now}] S3 ERROR: {ex.Message}{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}");
                throw;
            }
        }
    }
}
