using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using RealtimeChat.Interfaces;

namespace RealtimeChat.Services
{
    public class ImageService : IImageService
    {
        private readonly Cloudinary _cloudinary;
        public ImageService(IConfiguration configuration)
        {
            var account = new Account(
                configuration["CloudinarySettings:CloudName"],
                configuration["CloudinarySettings:ApiKey"],
                configuration["CloudinarySettings:ApiSecret"]
                );
            _cloudinary = new Cloudinary(account);
        }

        public async Task<string> UploadImageAsync(IFormFile file)
        {
            if (file.Length == 0)
                throw new ArgumentException("File is empty");

            await using var fileStream = file.OpenReadStream();

            var contentType = file.ContentType.ToLower();
            RawUploadParams uploadParams;

            if (contentType.StartsWith("video/"))
            {
                uploadParams = new VideoUploadParams
                {
                    File = new FileDescription(file.FileName, fileStream),
                    Folder = "Chatlify/Videos",
                    UseFilename = true,
                    UniqueFilename = true,
                    Overwrite = true,
                };
            }
            else if (contentType.StartsWith("image/"))
            {
                uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, fileStream),
                    Folder = "Chatlify/Images",
                    UseFilename = true,
                    UniqueFilename = true,
                    Overwrite = true,
                };
            }
            else if (contentType.StartsWith("application/") || contentType.StartsWith("text/"))
            {
                uploadParams = new RawUploadParams
                {
                    File = new FileDescription(file.FileName, fileStream),
                    Folder = "Chatlify/Files",
                    UseFilename = true,
                    UniqueFilename = true,
                    Overwrite = true,
                };
            }
            else
            {
                throw new ArgumentException("Unsupported file type");
            }

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);

            if (uploadResult.Error != null)
                throw new Exception(uploadResult.Error.Message);

            return uploadResult.SecureUrl.ToString();

        }
    }
}
