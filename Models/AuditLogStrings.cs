namespace SuperCoolWebServer.Models;

public static class AuditLogStrings
{
    public static class Actions
    {
        public const string AUTH_LOGIN_SUCCEEDED = "auth.login.succeeded";
        public const string AUTH_LOGIN_FAILED = "auth.login.failed";
        public const string AUTH_LOGOUT = "auth.logout";

        public const string USER_CREATED = "user.created";
        public const string USER_DELETED = "user.deleted";
        public const string USER_PASSWORD_CHANGED = "user.password.changed";
        public const string USER_PASSWORD_RESET_BY_ADMIN = "user.password.reset_by_admin";
        public const string USER_PERMISSIONS_CHANGED = "user.permissions.changed";

        public const string UPLOADED_FILE_MVC = "file.uploaded.mvc";
        public const string UPLOADED_FILE_TUS = "file.uploaded.tus";
        public const string FILE_OVERWRITTEN = "file.overwritten";
        public const string FILE_DELETED = "file.deleted";
        public const string FILE_DOWNLOADED = "file.downloaded";

        public const string LINK_CREATED = "link.created";
        public const string LINK_UPDATED = "link.updated";
        public const string LINK_DELETED = "link.deleted";

        public const string ACCESS_APP_CREATED = "access_app.created";
        public const string ACCESS_APP_LIMIT_CHANGED = "access_app.limit_changed";
        public const string ACCESS_APP_AUTHORIZATION_DENIED = "access_app.authorization_denied";

        public const string WOL_PACKET_SENT = "wol.packet_sent";
        public const string WOL_PACKET_FAILED = "wol.packet_failed";
    }

    public static class Entities
    {
        public const string USER = "user";
        public const string FILE = "file";
        public const string LINK = "link";
        public const string ACCESS_APP = "access_app";
        public const string WOL_DEVICE = "wol_device";
    }
}
