using Fundoo.User.Application.Services;
using Fundoo.Users.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace Fundoo.Users.Infrastructure.Email
{
    public class EmailService : IEmailService
    {
        private readonly HttpClient _httpClient;

        public EmailService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<EmailResponseModel> SendEmailAsync(EmailRequestModel request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/api/notification/send", request);

                if (!response.IsSuccessStatusCode)
                {
                    return new EmailResponseModel
                    {
                        Success = false,
                        Message = $"Notification service returned status code {(int)response.StatusCode}: {response.ReasonPhrase}"
                    };
                }

                return await response.Content.ReadFromJsonAsync<EmailResponseModel>()
                    ?? new EmailResponseModel
                    {
                        Success = false,
                        Message = "Notification service returned an empty response."
                    };
            }
            catch (HttpRequestException ex)
            {
                return new EmailResponseModel
                {
                    Success = false,
                    Message = $"Failed to communicate with Notification Service: {ex.Message}"
                };
            }
            catch (Exception ex)
            {
                return new EmailResponseModel
                {
                    Success = false,
                    Message = $"Unexpected error while sending email: {ex.Message}"
                };
            }
        }
    }
}
