using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Models;

namespace ProjectManager.Web.Services;

public sealed class UserLookupService(UserManager<ApplicationUser> userManager)
{
    private IReadOnlyList<ApplicationUser>? activeUsers;

    public async Task<string?> ResolveUserIdAsync(
        string? value,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var users = await LoadActiveUsersAsync(cancellationToken);
        var input = NormalizeForLookup(value);
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        return users
            .Where(user => Matches(user, value, input))
            .OrderByDescending(user => string.Equals(user.Id, value.Trim(), StringComparison.Ordinal))
            .ThenByDescending(user => string.Equals(user.UserName, value.Trim(), StringComparison.OrdinalIgnoreCase))
            .Select(user => user.Id)
            .FirstOrDefault();
    }

    public async Task<IReadOnlyList<string>> ResolveUserIdsAsync(
        string? value,
        CancellationToken cancellationToken)
    {
        var ids = new List<string>();
        foreach (var item in SplitNames(value))
        {
            var id = await ResolveUserIdAsync(item, cancellationToken);
            if (!string.IsNullOrWhiteSpace(id) &&
                !ids.Contains(id, StringComparer.Ordinal))
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    public static IEnumerable<string> SplitNames(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield break;
        }

        foreach (var item in value.Split(
                     [',', ';', '，', '；', '、', '\r', '\n', '\t'],
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!string.IsNullOrWhiteSpace(item))
            {
                yield return item.Trim();
            }
        }
    }

    public static string NormalizeForLookup(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value
            .Normalize(NormalizationForm.FormKC)
            .Trim()
            .ToLowerInvariant();
        var chars = new List<char>(normalized.Length);
        foreach (var ch in normalized)
        {
            if (char.IsWhiteSpace(ch) ||
                ch is '-' or '_' or '.' or '·' or '/' or '\\' or '(' or ')' or '（' or '）')
            {
                continue;
            }

            chars.Add(TraditionalToSimplified.TryGetValue(ch, out var mapped) ? mapped : ch);
        }

        return new string(chars.ToArray());
    }

    private async Task<IReadOnlyList<ApplicationUser>> LoadActiveUsersAsync(CancellationToken cancellationToken)
    {
        activeUsers ??= await userManager.Users
            .AsNoTracking()
            .Where(x => x.IsActive)
            .ToListAsync(cancellationToken);

        return activeUsers;
    }

    private static bool Matches(ApplicationUser user, string rawValue, string normalizedValue)
    {
        var trimmed = rawValue.Trim();
        if (string.Equals(user.Id, trimmed, StringComparison.Ordinal))
        {
            return true;
        }

        var candidates = new[]
        {
            user.UserName,
            user.NormalizedUserName,
            user.Email,
            user.NormalizedEmail,
            user.DisplayName
        };

        return candidates
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Any(x => string.Equals(
                NormalizeForLookup(x),
                normalizedValue,
                StringComparison.OrdinalIgnoreCase));
    }

    private static readonly IReadOnlyDictionary<char, char> TraditionalToSimplified =
        new Dictionary<char, char>
        {
            ['臺'] = '台',
            ['灣'] = '湾',
            ['雲'] = '云',
            ['龍'] = '龙',
            ['劉'] = '刘',
            ['張'] = '张',
            ['陳'] = '陈',
            ['黃'] = '黄',
            ['楊'] = '杨',
            ['趙'] = '赵',
            ['吳'] = '吴',
            ['鄭'] = '郑',
            ['馮'] = '冯',
            ['孫'] = '孙',
            ['馬'] = '马',
            ['歐'] = '欧',
            ['葉'] = '叶',
            ['許'] = '许',
            ['蘇'] = '苏',
            ['鄧'] = '邓',
            ['蕭'] = '萧',
            ['謝'] = '谢',
            ['羅'] = '罗',
            ['鐘'] = '钟',
            ['鍾'] = '钟',
            ['廖'] = '廖',
            ['賴'] = '赖',
            ['龔'] = '龚',
            ['盧'] = '卢',
            ['譚'] = '谭',
            ['賈'] = '贾',
            ['嚴'] = '严',
            ['薑'] = '姜',
            ['韓'] = '韩',
            ['廠'] = '厂',
            ['號'] = '号',
            ['員'] = '员',
            ['專'] = '专',
            ['項'] = '项',
            ['負'] = '负',
            ['責'] = '责',
            ['進'] = '进',
            ['說'] = '说',
            ['明'] = '明',
            ['測'] = '测',
            ['試'] = '试',
            ['維'] = '维',
            ['護'] = '护'
        };
}
