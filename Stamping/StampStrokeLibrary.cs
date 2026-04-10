#nullable enable
using Godot;

namespace MapStamp;

public static class StampStrokeLibrary
{
    public static Vector2[][] GetStrokes(string stampId)
    {
        return stampId switch
        {
            "dororong_mark" => DororongMark,
            "spiky_mark" => SpikyMark,
            _ => [],
        };
    }

    private static readonly Vector2[][] DororongMark =
    [
        [new(-30.0f, -2.0f), new(-28.0f, -12.0f), new(-22.0f, -20.0f), new(-12.0f, -26.0f), new(0.0f, -28.0f), new(12.0f, -27.0f), new(22.0f, -22.0f), new(28.0f, -14.0f), new(30.0f, -4.0f), new(27.0f, 4.0f), new(19.0f, 10.0f)],
        [new(-28.0f, 2.0f), new(-23.0f, 10.0f), new(-14.0f, 14.0f), new(-1.0f, 16.0f), new(12.0f, 16.0f), new(22.0f, 13.0f), new(29.0f, 7.0f)],
        [new(-10.0f, -20.0f), new(-17.0f, -27.0f), new(-25.0f, -22.0f), new(-29.0f, -11.0f), new(-26.0f, -2.0f)],
        [new(11.0f, -18.0f), new(20.0f, -24.0f), new(28.0f, -19.0f), new(31.0f, -10.0f), new(27.0f, -3.0f)],
        [new(20.0f, -20.0f), new(27.0f, -23.0f), new(33.0f, -17.0f), new(29.0f, -9.0f), new(22.0f, -12.0f)],
        [new(-9.0f, -2.0f), new(-5.0f, -2.0f)],
        [new(7.0f, -2.0f), new(11.0f, -2.0f)],
        [new(-2.0f, 5.0f), new(2.0f, 7.0f), new(7.0f, 5.0f)],
        [new(-18.0f, 16.0f), new(-20.0f, 26.0f)],
        [new(-2.0f, 16.0f), new(-4.0f, 26.0f)],
        [new(15.0f, 16.0f), new(14.0f, 26.0f)],
    ];

    private static readonly Vector2[][] SpikyMark =
    [
        [new(-42.0f, -12.0f), new(-36.0f, -28.0f), new(-24.0f, -40.0f), new(-8.0f, -45.0f), new(10.0f, -45.0f), new(27.0f, -41.0f), new(40.0f, -31.0f), new(47.0f, -17.0f), new(46.0f, -5.0f), new(39.0f, 1.0f)],
        [new(-24.0f, -7.0f), new(-17.0f, 4.0f), new(-13.0f, 18.0f), new(-5.0f, 30.0f), new(8.0f, 37.0f), new(23.0f, 37.0f), new(35.0f, 30.0f), new(41.0f, 18.0f), new(40.0f, 4.0f)],
        [new(-18.0f, -2.0f), new(-12.0f, -10.0f), new(-4.0f, -13.0f), new(6.0f, -13.0f), new(17.0f, -12.0f), new(28.0f, -8.0f)],
        [new(-8.0f, 2.0f), new(-4.0f, 2.0f)],
        [new(13.0f, 2.0f), new(17.0f, 2.0f)],
        [new(-15.0f, 11.0f), new(-10.0f, 15.0f)],
        [new(24.0f, 11.0f), new(19.0f, 15.0f)],
        [new(2.0f, 12.0f), new(7.0f, 17.0f), new(12.0f, 12.0f)],
        [new(-10.0f, 35.0f), new(-12.0f, 49.0f)],
        [new(3.0f, 36.0f), new(1.0f, 50.0f)],
        [new(18.0f, 35.0f), new(18.0f, 50.0f)],
        [new(-37.0f, 2.0f), new(-43.0f, 17.0f)],
    ];
}
