namespace TicketReservation.Api;

public class Reservation
{
	public required string UserId { get; init; }
	public required Seat[] Seats { get; init; }

	public class Seat
	{
		public required int Row { get; init; }
		public required int Col { get; init; }
	}
}
