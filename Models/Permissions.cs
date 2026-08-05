namespace SuperCoolWebServer.Models;

public enum Permissions : long
{
    // Used by filestore
    UploadFiles = 1 << 0,
    ListFiles = 1 << 1,
    // Used by redirector
    CreateLinks = 1 << 2,
    // Used by WOL controller
    UseWakeOnLan = 1 << 3,
    // Lets this user create or change the permissions of other users.
    ManageUsers = 1 << 4,
    Administrator = ~0,
}