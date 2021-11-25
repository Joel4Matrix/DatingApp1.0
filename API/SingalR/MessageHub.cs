using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs;
using API.Entities;
using API.Extensions;
using API.Interfaces;
using AutoMapper;
using Microsoft.AspNetCore.SignalR;

namespace API.SingalR
{
    public class MessageHub : Hub
    {
        private readonly IMapper _mapper;
        private readonly IMessageRepository _messageRepository;
        private readonly IUserRepository _userRepository;
        public MessageHub(IMessageRepository messageRepository, IMapper mapper, IUserRepository userRepository)
        {
            _userRepository = userRepository;
            _messageRepository = messageRepository;
            _mapper = mapper;
        }

        public override async Task OnConnectedAsync()
        {
            var httpContext = Context.GetHttpContext();
            var otherUser = httpContext.Request.Query["user"].ToString();
            var groupName = GetGroupName(Context.User.GetUsername(), otherUser);
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

            var messages = await _messageRepository.GetMessageThread(Context.User.GetUsername(), otherUser);

            await Clients.Group(groupName).SendAsync("RecieveMessageThread", messages);
        }

         public override async Task OnDisconnectedAsync(Exception exception)
        {
            //var group = await RemoveFromMessageGroup();
            //await Clients.Group(group.Name).SendAsync("UpdatedGroup", group);
            await base.OnDisconnectedAsync(exception);
        }

        private string GetGroupName(string caller, string other)
        {
            var stringCompare = string.CompareOrdinal(caller, other) < 0;
            return stringCompare ? $"{caller}-{other}" : $"{other}-{caller}";
        }

        public async Task SendMessage(CreateMessageDto createMessageDto)
        {
            var username = Context.User.GetUsername();

            if (username == createMessageDto.RecipientUsername.ToLower())
                throw new HubException("You cannot send messages to yourself");

            var sender = await _userRepository.GetUserByUsernameAsync(username);
            var recipient = await _userRepository.GetUserByUsernameAsync(createMessageDto.RecipientUsername);

            if (recipient == null) throw new HubException("Not found user");

            var message = new Message
            {
                Sender = sender,
                Recipient = recipient,
                SenderUsername = sender.UserName,
                RecipientUsername = recipient.UserName,
                Content = createMessageDto.Content
            };

            

            // var group = await _messageRepository.GetMessageGroup(groupName);

            // if (group.Connections.Any(x => x.Username == recipient.UserName))
            // {
            //     message.DateRead = DateTime.UtcNow;
            // }
            // else
            // {
            //     var connections = await _tracker.GetConnectionsForUser(recipient.UserName);
            //     if (connections != null)
            //     {
            //         await _presenceHub.Clients.Clients(connections).SendAsync("NewMessageReceived",
            //             new { username = sender.UserName, knownAs = sender.KnownAs });
            //     }
            // }

            _messageRepository.AddMessage(message);

            if(await _messageRepository.SaveAllAsync()) 
            {
                var group = GetGroupName(sender.UserName, recipient.UserName);
                await Clients.Group(group).SendAsync("NewMessage", _mapper.Map<MessageDto>(message));
            } 
            // if (await _unitOfWork.Complete())
            // {
            //     await Clients.Group(groupName).SendAsync("NewMessage", _mapper.Map<MessageDto>(message));
            // }

        }
    }
}