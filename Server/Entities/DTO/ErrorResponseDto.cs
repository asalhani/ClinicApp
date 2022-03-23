namespace Entities.DTO
{
    public class ErrorResponseDto
    {
        public string ErrorId { get; set; }
        public int StatusCode { get; set; }
        public string ErrorMessage { get; set; }
        public string ErrorDetails { get; set; }
    }
}