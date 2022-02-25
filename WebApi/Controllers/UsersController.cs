using System;
using System.Linq;
using AutoMapper;
using Game.Domain;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using WebApi.Models;

namespace WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : Controller
    {
        private readonly IUserRepository userRepository;
        private readonly IMapper mapper;
        
        // Чтобы ASP.NET положил что-то в userRepository требуется конфигурация
        public UsersController(IUserRepository userRepository, IMapper mapper)
        {
            this.userRepository = userRepository;
            this.mapper = mapper;
        }

        [HttpGet("{userId}", Name = nameof(GetUserById))]
        [Produces("application/json", "application/xml")]
        public ActionResult<UserDto> GetUserById([FromRoute] Guid userId)
        {
            var userEntity = userRepository.FindById(userId);
            if (userEntity == null)
                return NotFound();
            
            var userDto = mapper.Map<UserDto>(userEntity);
            return Ok(userDto);
        }

        [HttpPost]
        [Produces("application/json", "application/xml")]
        [Consumes("application/json")]
        public IActionResult CreateUser([FromBody] UserToCreateDto user)
        {
            if (user == null)
                return BadRequest();
            if (!ModelState.IsValid)
                return UnprocessableEntity(ModelState);
            
            var userEntity = mapper.Map<UserEntity>(user);
            var createdUserEntity = userRepository.Insert(userEntity);
            
            return CreatedAtRoute(
                nameof(GetUserById),
                new {userId = createdUserEntity.Id},
                createdUserEntity.Id);
        }

        [HttpPut("{userId}")]
        [Consumes("application/json")]
        [Produces("application/json")]
        public IActionResult UpdateUser([FromBody]UserToUpdateDto userToUpdateDto, [FromRoute]Guid userId)
        {
            if (userToUpdateDto == null || userId == Guid.Empty)
                return BadRequest();
            if (!TryValidateModel(ModelState))
                return UnprocessableEntity(ModelState);
            
            var userEntity = mapper.Map(userToUpdateDto, new UserEntity(userId));
            userRepository.UpdateOrInsert(userEntity, out var isInserted);

            return isInserted
                ? CreatedAtRoute(nameof(GetUserById),
                    new {userId = userId},
                    userId)
                : NoContent();
        }

        [HttpPatch("{userId}")]
        [Produces("application/json")]
        public IActionResult PartitiallyUpdateUser([FromBody] JsonPatchDocument<UserToUpdateDto> userToUpdateDtoPatch, [FromRoute] Guid userId)
        {
            if (userToUpdateDtoPatch == null)
                return BadRequest();
            if (userRepository.FindById(userId) == null)
                return NotFound();

            var userToUpdateDto = new UserToUpdateDto();
            userToUpdateDtoPatch.ApplyTo(userToUpdateDto, ModelState);
            if (!TryValidateModel(userToUpdateDto))
                return UnprocessableEntity(ModelState);

            var userEntity = mapper.Map(userToUpdateDto, new UserEntity(userId));
            userRepository.Update(userEntity);
            return NoContent();
        }

        [HttpDelete("{userId}")]
        public IActionResult DeleteUser([FromRoute] Guid userId)
        {
            if (userRepository.FindById(userId) == null)
                return NotFound();
            
            userRepository.Delete(userId);
            return NoContent();
        }
    }
}