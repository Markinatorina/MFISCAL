using MFISCAL_BLL.Models;
using MFISCAL_BLL.Services;
using MFISCAL_DAL.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MFISCAL_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InvoiceController : ControllerBase
    {
        private readonly InvoiceService _invoiceService;

        public InvoiceController(InvoiceService invoiceService)
        {
            _invoiceService = invoiceService ?? throw new ArgumentNullException(nameof(invoiceService));
        }

        [HttpGet]
        public ActionResult<IEnumerable<InvoiceDTO>> GetAll()
        {
            var invoices = _invoiceService.GetAll();
            return Ok(invoices);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<InvoiceDTO>> GetById(Guid id)
        {
            var invoice = await _invoiceService.GetByIdAsync(id);
            if (invoice == null) return NotFound();
            return Ok(invoice);
        }

        [HttpPost]
        public async Task<ActionResult<InvoiceDTO>> Create([FromBody] InvoiceDTO dto)
        {
            var created = await _invoiceService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<InvoiceDTO>> Update(Guid id, [FromBody] InvoiceDTO dto)
        {
            var updated = await _invoiceService.UpdateAsync(id, dto);
            if (updated == null) return NotFound();
            return Ok(updated);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _invoiceService.DeleteAsync(id);
            return NoContent();
        }

        [HttpPost("{id}/publish")]
        public async Task<ActionResult<InvoiceDTO>> Publish(Guid id, [FromBody] DateTimeOffset? publishedAt)
        {
            var publishedInvoice = await _invoiceService.PublishAsync(id, publishedAt);
            if (publishedInvoice == null) return NotFound();
            return Ok(publishedInvoice);
        }
    }
}
