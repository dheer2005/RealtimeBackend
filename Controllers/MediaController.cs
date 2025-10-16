using Microsoft.AspNetCore.Mvc;
using RealtimeChat.Dtos;
using RealtimeChat.Interfaces;

namespace RealtimeChat.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MediaController : ControllerBase
    {
        private readonly IImageService _imageService;
        public MediaController(IImageService imageService)
        {
            _imageService = imageService;
        }

        [HttpPost("uploadMedia")]
        public async Task<IActionResult> UploadMedia([FromForm] uplodaMediaDto file)
        {
            if (file == null || file.File.Length == 0)
                return BadRequest(new { message = "File not found" });

            var url = await _imageService.UploadImageAsync(file.File);
            return Ok(new { url });
        }
    }
}
