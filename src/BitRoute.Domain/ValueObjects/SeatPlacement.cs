namespace BitRoute.Domain.ValueObjects;

/// <summary>
/// Where a single seat physically sits inside a vehicle, plus the label the passenger sees.
/// <para>
/// <c>Row</c> is 1-based and counts from the front of the vehicle. <c>Column</c> is 1-based and
/// counts left-to-right across the FULL width of the cabin, including the position the aisle
/// occupies. A 2+2 coach therefore places seats at columns 1, 2, 4, 5 and leaves column 3 empty
/// as the aisle. Using an integer column (rather than the letter in <c>Number</c>) is what makes
/// the aisle expressible and lets a row have gaps.
/// </para>
/// </summary>
public readonly record struct SeatPlacement(string Number, int Row, int Column);
