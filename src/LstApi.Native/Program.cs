// See https://aka.ms/new-console-template for more information

using LstApi.Model;
using LstApi.Native;
using LstApi.Native.Platforms;

var lstRules = TimeZoneRuleModule.calculateRules(new TimeZoneOptions(
    new Geolocation(-43, 172),
    OffsetResolution.FifteenMinutes,
    TimeSpan.FromHours(-4),
    TimeSpan.Zero));

var lstAdjustments = TimeZoneAdjustmentModule.calculateAdjustments(lstRules, DateTimeOffset.UtcNow);

var platformAdjustmentRules = lstAdjustments
    .Pairwise()
    .Select(pair =>
    {
        var (currentAdjustment, nextAdjustment) = pair;
        return TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            currentAdjustment.Timestamp.ToDateTimeOffset(currentAdjustment.Offset).Date,
            nextAdjustment.Timestamp.ToDateTimeOffset(nextAdjustment.Offset).Date.AddDays(-1),
            currentAdjustment.Offset,
            TimeZoneInfo.TransitionTime.CreateFixedDateRule(
                DateTime.MinValue.Add(currentAdjustment.Rule.Start.TimeOfDay.ToTimeSpan()),
                currentAdjustment.Rule.Start.Month,
                currentAdjustment.Rule.Start.Day),
            TimeZoneInfo.TransitionTime.CreateFixedDateRule(
                DateTime.MinValue.Add(currentAdjustment.Rule.End.TimeOfDay.ToTimeSpan()),
                currentAdjustment.Rule.End.Month,
                currentAdjustment.Rule.End.Day));
    })
    .ToArray();

var tz = TimeZoneInfo.CreateCustomTimeZone(
    "Local Sun Time",
    TimeSpan.Zero,
    "(DYNAMIC) Local Sun Time",
    "Local Sun Time",
    "Local Sun Time",
    platformAdjustmentRules);

var repository = ITimeZoneInfoRepository.Create();

await repository.Save(tz);

var tz0 = TimeZoneInfo.FindSystemTimeZoneById("Local Sun Time");
Console.WriteLine(tz0);