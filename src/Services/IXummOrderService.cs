﻿using System;
using System.Threading.Tasks;
using Nop.Core.Domain.Orders;
using Nop.Services.Payments;

namespace Nop.Plugin.Payments.Xumm.Services;

public interface IXummOrderService
{
    Task<string> GetPaymentRedirectUrlAsync(PostProcessPaymentRequest postProcessPaymentRequest);
    Task<string> GetRefundRedirectUrlAsync(Guid orderGuid, decimal amountToRefund, bool isPartialRefund);
    Task<Order> ProcessOrderAsync(Guid orderGuid);
    Task<Order> ProcessRefundAsync(Guid orderGuid, int? count);
    Task<RefundPaymentResult> ProcessRefundPaymentRequestAsync(RefundPaymentRequest refundPaymentRequest);
}
