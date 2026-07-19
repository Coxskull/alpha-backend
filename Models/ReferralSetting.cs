using System;

namespace Alpha.API.Models;

public class ReferralSetting
{
    public Guid Id { get; set; }

    public string SettingKey { get; set; } = string.Empty;

    public string SettingValue { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime UpdatedAt { get; set; }
}