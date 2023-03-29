# Lst-Api

This is a simple API for generating a timezone based on your local suntime (LST).

Remember! Your local suntime is personal. It's not (necessarily) for co-ordination with others. If it has a category, then it is productivity tool. It's about how you orientate yourself around your day, how you get through the changing seasons without fighting your body's natural rhythms.

With that in mind, this API is unopinionated and gives you various options.

For example, personally, I like to set 12:00am to be sunrise (extraOffsetMinutes = 0). But if you want something that's _kind of_ close to your legislated timezone, then you could set it back ~8 hours (-720).

## Getting Started

The API is not currently hosted anyway. You're free to clone this repo and run it yourself.

```shell
cd src/LstApi && dotnet run
```

## Usage

### GET /rules

Returns the list of timezone rules for any given year.

**Parameters**

| Parameter                    | Type                                                                                    | Description                                                                                                                                           | Default        |
| ---------------------------- | --------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------- | -------------- |
| latitude                     | `float:[-90,90]`                                                                        | Latitude of your location, used for calculating UTC sunrise time                                                                                      | **required**   |
| longitude                    | `float:[-180,180]`                                                                      | Longitude of your location, used for calculating UTC sunrise time                                                                                     | **required**   |
| offsetResolution             | `exact \| five_minutes \| ten_minutes \| fifteen_minutes \| thirty_minutes \| one_hour` | Determines the rounding of the UTC offset.                                                                                                            | `five_minutes` |
| extraOffsetMinutes           | `int`                                                                                   | Specifies the extra offset to apply, relative to sunrise ~= 12:00am.                                                                                  | `0`            |
| adjustmentEventOffsetMinutes | `int`                                                                                   | Specifies the time of the day to apply a timezone adjustment e.g. -240 is 8:00pm (4 hours before midnight). This is affected by `extraOffsetMinutes`. | `-240`         |

**Response**

```javascript
// application/json
[
  start: {
    month: int,
    day: int,
    timeOfDay: string
  },
  end: {
    month: int,
    day: int,
    timeOfDay: string
  },
  offset: string
]

```

### GET /adjustments

Enumerates the list of actual adjustment events, from a given date. The first adjustment returned will be the one that is currently in effect.

**Parameters**

Supports all the parameters of `/rules` plus:

| Parameter | Type             | Description                                  | Default                                                 |
| --------- | ---------------- | -------------------------------------------- | ------------------------------------------------------- |
| asAt      | `dateTimeString` | The timestamp to calculate adjustments from. | _the current UTC timestamp_ e.g. `2020-01-01T00:00:00Z` |
