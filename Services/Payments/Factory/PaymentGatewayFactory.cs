using Alpha.API.Services.Payments.Interfaces;
using System;
using System.Collections.Generic;

namespace Alpha.API.Services.Payments.Factory;

public class PaymentGatewayFactory
{
	private readonly IEnumerable<IPaymentGateway> _gateways;

	public PaymentGatewayFactory(
		IEnumerable<IPaymentGateway> gateways)
	{
		_gateways = gateways;
	}

	public IPaymentGateway Get(string gateway)
	{
		return _gateways.FirstOrDefault(x =>
			x.Name.Equals(
				gateway,
				StringComparison.OrdinalIgnoreCase))
			?? throw new Exception($"Gateway {gateway} not found.");
	}
}