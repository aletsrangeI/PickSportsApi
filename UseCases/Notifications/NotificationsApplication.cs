using Common;
using Domain.Entities;
using DTO.Notifications;
using FluentValidation;
using Interface.Persistence;
using Interface.UseCases;
using Validator.Notifications;

namespace UseCases.Notifications;

public class NotificationsApplication : INotificationsApplication
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IWebPushNotificationService _webPushService;
    private readonly PushSubscriptionRequestValidator _validator;

    public NotificationsApplication(
        IUnitOfWork unitOfWork,
        IWebPushNotificationService webPushService,
        PushSubscriptionRequestValidator validator)
    {
        _unitOfWork = unitOfWork;
        _webPushService = webPushService;
        _validator = validator;
    }

    public async Task<Response<bool>> SubscribeAsync(int userId, PushSubscriptionRequestDto request)
    {
        var response = new Response<bool>();

        var validationResult = await _validator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            response.isSuccess = false;
            response.Message = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
            return response;
        }

        var user = await _unitOfWork.Users.GetAsync(userId);
        if (user == null)
        {
            response.isSuccess = false;
            response.Message = "Usuario no encontrado.";
            return response;
        }

        var endpoint = request.Endpoint.Trim();
        var p256dh = request.GetP256dh();
        var auth = request.GetAuth();

        var existing = await _unitOfWork.PushSubscriptions.GetByEndpointAsync(endpoint);
        if (existing != null)
        {
            existing.UserId = userId;
            existing.P256dhKey = p256dh;
            existing.AuthKey = auth;
            existing.DeviceDescription = request.DeviceDescription ?? existing.DeviceDescription;
            existing.Active = true;
            await _unitOfWork.PushSubscriptions.UpdateAsync(existing);
        }
        else
        {
            var newSub = new PushSubscription
            {
                UserId = userId,
                Endpoint = endpoint,
                P256dhKey = p256dh,
                AuthKey = auth,
                DeviceDescription = request.DeviceDescription,
                Active = true,
                CreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.PushSubscriptions.InsertAsync(newSub);
        }

        await _unitOfWork.Save();

        response.isSuccess = true;
        response.Data = true;
        response.Message = "Suscripción a notificaciones push registrada exitosamente.";
        return response;
    }

    public async Task<Response<bool>> UnsubscribeAsync(int userId, UnsubscribePushRequestDto request)
    {
        var response = new Response<bool>();

        if (string.IsNullOrWhiteSpace(request.Endpoint))
        {
            response.isSuccess = false;
            response.Message = "El endpoint es requerido para desuscribirse.";
            return response;
        }

        var existing = await _unitOfWork.PushSubscriptions.GetByEndpointAsync(request.Endpoint.Trim());
        if (existing != null)
        {
            existing.Active = false;
            await _unitOfWork.PushSubscriptions.UpdateAsync(existing);
            await _unitOfWork.Save();
        }

        response.isSuccess = true;
        response.Data = true;
        response.Message = "Suscripción desactivada correctamente.";
        return response;
    }

    public Task<Response<VapidPublicKeyDto>> GetVapidPublicKeyAsync()
    {
        var response = new Response<VapidPublicKeyDto>();
        var key = _webPushService.GetVapidPublicKey();

        if (string.IsNullOrWhiteSpace(key))
        {
            response.isSuccess = false;
            response.Message = "Las notificaciones Web Push no están configuradas en este servidor.";
            return Task.FromResult(response);
        }

        response.isSuccess = true;
        response.Data = new VapidPublicKeyDto { PublicKey = key };
        response.Message = "Clave pública VAPID obtenida con éxito.";
        return Task.FromResult(response);
    }

    public async Task<Response<bool>> SendTestNotificationAsync(int userId)
    {
        var response = new Response<bool>();
        var sent = await _webPushService.SendTestNotificationAsync(userId);

        if (!sent)
        {
            response.isSuccess = false;
            response.Message = "No se pudo enviar la notificación. Verifica que tu dispositivo tenga permisos activos.";
            return response;
        }

        response.isSuccess = true;
        response.Data = true;
        response.Message = "Notificación enviada correctamente a tu dispositivo.";
        return response;
    }
}
