namespace SuperCoolWebServer.Models;

public static class AuditLogStrings
{
    public static class Actions
    {
        public const string UPLOADED_FILE_MVC = "uploadfile.mvc";
        public const string UPLOADED_FILE_TUS = "uploadfile.tus";   
    }

    public static class Entities
    {
        public const string FILE = "file";
    }
}