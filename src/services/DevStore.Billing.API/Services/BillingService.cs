using System;
using System.Linq;
using System.Threading.Tasks;
using DevStore.Billing.API.Facade;
using DevStore.Billing.API.Models;
using DevStore.Core.DomainObjects;
using DevStore.Core.Messages.Integration;
using FluentValidation.Results;

namespace DevStore.Billing.API.Services
{
    public class BillingService : IBillingService
    {
        private readonly IPaymentFacade _paymentFacade;
        private readonly IPaymentRepository _paymentRepository;

        public BillingService(IPaymentFacade paymentFacade, 
                                IPaymentRepository paymentRepository)
        {
            _paymentFacade = paymentFacade;
            _paymentRepository = paymentRepository;
        }

        public async Task<ResponseMessage> AuthorizeTransaction(Payment payment)
        {
            var transacao = await _paymentFacade.AuthorizePayment(payment);
            var validationResult = new ValidationResult();

            if (transacao.TransactionStatus != TransactionStatus.Authorized)
            {
                validationResult.Errors.Add(new ValidationFailure("Payment",
                        "Payment recusado, entre em contato com a sua operadora de cartão"));

                return new ResponseMessage(validationResult);
            }

            payment.AdicionarTransacao(transacao);
            _paymentRepository.AddPayment(payment);

            if (!await _paymentRepository.UnitOfWork.Commit())
            {
                validationResult.Errors.Add(new ValidationFailure("Payment",
                    "Houve um erro ao realizar o payment."));

                // Cancelar payment no gateway
                await CancelTransaction(payment.OrderId);

                return new ResponseMessage(validationResult);
            }

            return new ResponseMessage(validationResult);
        }

        public async Task<ResponseMessage> GetTransaction(Guid pedidoId)
        {
            var transacoes = await _paymentRepository.GetTransactionsByOrderId(pedidoId);
            var transacaoAutorizada = transacoes?.FirstOrDefault(t => t.TransactionStatus == TransactionStatus.Authorized);
            var validationResult = new ValidationResult();

            if (transacaoAutorizada == null) throw new DomainException($"Transação não encontrada para o pedido {pedidoId}");

            var transacao =  await _paymentFacade.CapturePayment(transacaoAutorizada);

            if (transacao.TransactionStatus != TransactionStatus.Paid)
            {
                validationResult.Errors.Add(new ValidationFailure("Payment",
                    $"Não foi possível capturar o payment do pedido {pedidoId}"));

                return new ResponseMessage(validationResult);
            }

            transacao.PaymentId = transacaoAutorizada.PaymentId;
            _paymentRepository.AddTransaction(transacao);

            if (!await _paymentRepository.UnitOfWork.Commit())
            {
                validationResult.Errors.Add(new ValidationFailure("Payment",
                    $"Não foi possível persistir a captura do payment do pedido {pedidoId}"));

                return new ResponseMessage(validationResult);
            }

            return new ResponseMessage(validationResult);
        }

        public async Task<ResponseMessage> CancelTransaction(Guid pedidoId)
        {
            var transacoes = await _paymentRepository.GetTransactionsByOrderId(pedidoId);
            var transacaoAutorizada = transacoes?.FirstOrDefault(t => t.TransactionStatus == TransactionStatus.Authorized);
            var validationResult = new ValidationResult();

            if (transacaoAutorizada == null) throw new DomainException($"Transação não encontrada para o pedido {pedidoId}");

            var transacao = await _paymentFacade.CancelAuthorization(transacaoAutorizada);

            if (transacao.TransactionStatus != TransactionStatus.Canceled)
            {
                validationResult.Errors.Add(new ValidationFailure("Payment",
                    $"Não foi possível cancelar o payment do pedido {pedidoId}"));

                return new ResponseMessage(validationResult);
            }

            transacao.PaymentId = transacaoAutorizada.PaymentId;
            _paymentRepository.AddTransaction(transacao);

            if (!await _paymentRepository.UnitOfWork.Commit())
            {
                validationResult.Errors.Add(new ValidationFailure("Payment",
                    $"Não foi possível persistir o cancelamento do payment do pedido {pedidoId}"));

                return new ResponseMessage(validationResult);
            }

            return new ResponseMessage(validationResult);
        }
    }
}