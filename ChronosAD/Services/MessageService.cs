using ChronosAD.Data;
using ChronosAD.Models;

namespace ChronosAD.Services;

public class MessageService
{
    private readonly DatabaseService _db;
    public MessageService(DatabaseService db) => _db = db;

    public void Send(string sid, string message) => _db.SendMessage(sid, message);
    public List<Message> GetForUser(string sid) => _db.GetMessagesForUser(sid);
    public List<Message> GetAll() => _db.GetAllMessages();
    public void Respond(int messageId, string response) => _db.RespondToMessage(messageId, response);
}
