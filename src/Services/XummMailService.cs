using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nop.Core;
using Nop.Core.Domain.Messages;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Payments;

namespace Nop.Plugin.Payments.Xumm.Services
{
    public class XummMailService : IXummMailService
    {
        private readonly IStoreContext _storeContext;
        private readonly IEmailAccountService _emailAccountService;
        private readonly ILocalizationService _localizationService;
        private readonly ILanguageService _languageService;
        private readonly IMessageTemplateService _messageTemplateService;
        private readonly IMessageTokenProvider _messageTokenProvider;
        private readonly IQueuedEmailService _queuedEmailService;
        private readonly ITokenizer _tokenizer;
        private readonly EmailAccountSettings _emailAccountSettings;

        public XummMailService(IStoreContext storeContext,
            IEmailAccountService emailAccountService,
            ILocalizationService localizationService,
            ILanguageService languageService,
            IMessageTemplateService messageTemplateService,
            IMessageTokenProvider messageTokenProvider,
            IQueuedEmailService queuedEmailService,
            ITokenizer tokenizer,
            EmailAccountSettings emailAccountSettings)
        {
            _storeContext = storeContext;
            _emailAccountService = emailAccountService;
            _localizationService = localizationService;
            _languageService = languageService;
            _messageTemplateService = messageTemplateService;
            _messageTokenProvider = messageTokenProvider;
            _queuedEmailService = queuedEmailService;
            _tokenizer = tokenizer;
            _emailAccountSettings = emailAccountSettings;
        }

        public async Task<IList<int>> SendRefundMailToStoreOwnerAsync(RefundPaymentRequest refundPaymentRequest, int languageId)
        {
            var store = await _storeContext.GetCurrentStoreAsync();
            languageId = await EnsureLanguageIsActiveAsync(languageId, store.Id);

            var messageTemplates = await GetActiveMessageTemplatesAsync(XummDefaults.Mail.RefundEmailTemplateSystemName, store.Id);
            if (!messageTemplates.Any())
                return new List<int>();

            var tokens = new List<Token>
            {
                new Token("Order.RefundAmount", "1337 XRP"),
                new Token("Order.RefundUrl", "https://xumm.app"),
            };

            return await messageTemplates.SelectAwait(async messageTemplate =>
            {
                var emailAccount = await GetEmailAccountOfMessageTemplateAsync(messageTemplate, languageId);

                await _messageTokenProvider.AddStoreTokensAsync(tokens, store, emailAccount);
                await _messageTokenProvider.AddOrderTokensAsync(tokens, refundPaymentRequest.Order, languageId);

                return await SendNotificationAsync(messageTemplate, emailAccount, languageId, tokens);
            }).ToListAsync();
        }

        private async Task<int> EnsureLanguageIsActiveAsync(int languageId, int storeId)
        {
            var language = await _languageService.GetLanguageByIdAsync(languageId);
            if (language == null || !language.Published)
            {
                language = (await _languageService.GetAllLanguagesAsync(storeId: storeId)).FirstOrDefault(x => x.Published);
            }

            if (language == null)
            {
                throw new Exception("No active language could be loaded");
            }

            return language.Id;
        }

        private async Task<EmailAccount> GetEmailAccountOfMessageTemplateAsync(MessageTemplate messageTemplate, int languageId)
        {
            var emailAccountId = await _localizationService.GetLocalizedAsync(messageTemplate, mt => mt.EmailAccountId, languageId);
            //some 0 validation (for localizable "Email account" dropdownlist which saves 0 if "Standard" value is chosen)
            if (emailAccountId == 0)
            {
                emailAccountId = messageTemplate.EmailAccountId;
            }

            var emailAccount = (await _emailAccountService.GetEmailAccountByIdAsync(emailAccountId) ??
                                await _emailAccountService.GetEmailAccountByIdAsync(_emailAccountSettings.DefaultEmailAccountId)) ??
                               (await _emailAccountService.GetAllEmailAccountsAsync()).FirstOrDefault();
            return emailAccount;
        }

        private async Task<IList<MessageTemplate>> GetActiveMessageTemplatesAsync(string messageTemplateName, int storeId)
        {
            var messageTemplates = await _messageTemplateService.GetMessageTemplatesByNameAsync(messageTemplateName, storeId);
            return messageTemplates?.Where(messageTemplate => messageTemplate.IsActive).ToList() ?? new List<MessageTemplate>();
        }

        private async Task<int> SendNotificationAsync(MessageTemplate messageTemplate, EmailAccount emailAccount, int languageId, IList<Token> tokens)
        {
            if (messageTemplate == null)
                throw new ArgumentNullException(nameof(messageTemplate));

            if (emailAccount == null)
                throw new ArgumentNullException(nameof(emailAccount));

            // Retrieve localized message template data
            var bcc = await _localizationService.GetLocalizedAsync(messageTemplate, mt => mt.BccEmailAddresses, languageId);
            var subject = await _localizationService.GetLocalizedAsync(messageTemplate, mt => mt.Subject, languageId);
            var body = await _localizationService.GetLocalizedAsync(messageTemplate, mt => mt.Body, languageId);

            // Replace subject and body tokens
            var subjectReplaced = _tokenizer.Replace(subject, tokens, false);
            var bodyReplaced = _tokenizer.Replace(body, tokens, true);

            var name = CommonHelper.EnsureMaximumLength(emailAccount.DisplayName, 300);

            var email = new QueuedEmail
            {
                Priority = QueuedEmailPriority.High,
                From = emailAccount.Email,
                FromName = name,
                To = emailAccount.Email,
                ToName = name,
                CC = string.Empty,
                Bcc = bcc,
                Subject = subjectReplaced,
                Body = bodyReplaced,
                AttachedDownloadId = messageTemplate.AttachedDownloadId,
                CreatedOnUtc = DateTime.UtcNow,
                EmailAccountId = emailAccount.Id,
                DontSendBeforeDateUtc = !messageTemplate.DelayBeforeSend.HasValue ? null
                    : (DateTime?)(DateTime.UtcNow + TimeSpan.FromHours(messageTemplate.DelayPeriod.ToHours(messageTemplate.DelayBeforeSend.Value)))
            };

            await _queuedEmailService.InsertQueuedEmailAsync(email);
            return email.Id;
        }
    }
}
