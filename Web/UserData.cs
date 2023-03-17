#pragma warning disable 0649
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Rbx2Source.Web
{
    public enum AvatarType { R6, R15, Unknown }

    public class AvatarScale
    {
        public float Width;
        public float Height;
        public float Head;
        public float Depth;

        public float Proportion;
        public float BodyType;
    }

    public class UserApiResponse
    {
        public UserInfo[] Data { get; set; }
    }

    public class UserInfo
    {
        public long Id { get; set; }
        public bool HasVerifiedBadge { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }
    }



    public class WebBodyColors
    {
        public int HeadColorId;
        public int LeftArmColorId;
        public int RightArmColorId;
        public int LeftLegColorId;
        public int RightLegColorId;
        public int TorsoColorId;
    }

    public class WrappedAssetType
    {
        public AssetType Id;
        public string Name;
    }

    public class AssetInfo
    {
        public long Id;
        public string Name;

        public WrappedAssetType AssetType;
        public AssetType Type => AssetType.Id;
    }

    public class UserAvatar
    {
        public bool UserExists;
        public UserInfo UserInfo;

        public AvatarScale Scales;
        public AvatarType PlayerAvatarType;

        public WebBodyColors BodyColors;
        public AssetInfo[] Assets;

        private static UserAvatar createUserAvatar(UserInfo info)
        {
            UserAvatar avatar = WebUtility.DownloadAvatarApiJSON<UserAvatar>($"/v1/users/{info.Id}/avatar");
            avatar.UserExists = true;
            avatar.UserInfo = info;

            return avatar;
        }

        public static UserAvatar FromUserId(long userId)
        {
            try
            {
                UserInfo info = WebUtility.DownloadUsersApiJSON<UserInfo>("v1/users/" + userId);
                return createUserAvatar(info);
            }
            catch
            {
                return new UserAvatar();
            }
        }

        public static UserAvatar FromUsername(string userName)
{
    try
    {
        var postData = JsonConvert.SerializeObject(new
        {
            usernames = new[] { userName },
            excludeBannedUsers = true
        });
        string subAddress = $"v1/usernames/users";
        UserInfo info = WebUtility.PostUsersApiJSON<UserInfo>(subAddress, postData);

        // Check if data is empty
        if (info.data == null || info.data.Length == 0)
        {
            return new UserAvatar();
        }

        return createUserAvatar(info);
    }
    catch (Exception ex)
    {
        return new UserAvatar();
    }
}


    }
}
