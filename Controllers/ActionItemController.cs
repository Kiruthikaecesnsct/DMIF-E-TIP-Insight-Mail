using InsightMail.Models;
using InsightMail.Services;
using Microsoft.AspNetCore.Mvc;

namespace InsightMail.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class ActionItemsController : ControllerBase
    {
        private readonly IActionItemRepository _repository;

        public ActionItemsController(IActionItemRepository repository)
        {
            _repository = repository;
        }

        // GET: api/v1/actionitems
        [HttpGet]
        public async Task<ActionResult<List<ActionItem>>> GetAll()
        {
            var extracted = await _repository.GetByStatusAsync(ActionItemStatus.Extracted);
            var confirmed = await _repository.GetByStatusAsync(ActionItemStatus.Confirmed);
            var completed = await _repository.GetByStatusAsync(ActionItemStatus.Completed);

            var all = extracted
                .Concat(confirmed)
                .Concat(completed)
                .ToList();

            return Ok(all);
        }

        // GET: api/v1/actionitems/email/{emailId}
        [HttpGet("email/{emailId}")]
        public async Task<ActionResult<List<ActionItem>>> GetByEmail(string emailId)
        {
            var items = await _repository.GetByEmailIdAsync(emailId);
            return Ok(items);
        }

        // PUT: api/v1/actionitems/{id}/confirm
        [HttpPut("{id}/confirm")]
        public async Task<ActionResult> ConfirmTask(string id)
        {
            var item = await _repository.GetByIdAsync(id);

            if (item == null)
                return NotFound();

            item.Status = ActionItemStatus.Confirmed;
            item.ConfirmedDate = DateTime.UtcNow;

            await _repository.UpdateAsync(item);

            return Ok(item);
        }

        // PUT: api/v1/actionitems/{id}/complete
        [HttpPut("{id}/complete")]
        public async Task<ActionResult> CompleteTask(string id)
        {
            var item = await _repository.GetByIdAsync(id);

            if (item == null)
                return NotFound();

            item.Status = ActionItemStatus.Completed;
            item.CompletedDate = DateTime.UtcNow;

            await _repository.UpdateAsync(item);

            return Ok(item);
        }

        // DELETE: api/v1/actionitems/{id}
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteTask(string id)
        {
            await _repository.DeleteAsync(id);
            return NoContent();
        }
    }
}