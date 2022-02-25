using System;
using System.Linq;
using AutoMapper;
using Game.Domain;
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
        [Produces("application/json")]
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
    }
}