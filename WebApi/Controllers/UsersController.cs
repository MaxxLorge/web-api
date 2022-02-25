using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using Game.Domain;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Newtonsoft.Json;
using Swashbuckle.AspNetCore.Annotations;
using WebApi.Models;

namespace WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : Controller
    {
        private readonly IUserRepository userRepository;
        private readonly IMapper mapper;
        private readonly LinkGenerator linkGenerator;
        
        // Чтобы ASP.NET положил что-то в userRepository требуется конфигурация
        public UsersController(IUserRepository userRepository, IMapper mapper, LinkGenerator linkGenerator)
        {
            this.userRepository = userRepository;
            this.mapper = mapper;
            this.linkGenerator = linkGenerator;
        }

        /// <summary>
        /// Получить пользователя
        /// </summary>
        /// <param name="userId">Идентификатор пользователя</param>
        [HttpGet("{userId}", Name = nameof(GetUserById))]
        [HttpHead("{userId}")]
        [Produces("application/json", "application/xml")]
        [SwaggerResponse(200, "OK", typeof(UserDto))]
        [SwaggerResponse(404, "Пользователь не найден")]
        public ActionResult<UserDto> GetUserById([FromRoute] Guid userId)
        {
            var userEntity = userRepository.FindById(userId);
            if (userEntity == null)
                return NotFound();
            
            var userDto = mapper.Map<UserDto>(userEntity);
            return Ok(userDto);
        }

        /// <summary>
        /// Создать пользователя
        /// </summary>
        /// <remarks>
        /// Пример запроса:
        ///
        ///     POST /api/users
        ///     {
        ///        "login": "johndoe375",
        ///        "firstName": "John",
        ///        "lastName": "Doe"
        ///     }
        ///
        /// </remarks>
        /// <param name="user">Данные для создания пользователя</param>
        [HttpPost]
        [Consumes("application/json")]
        [Produces("application/json", "application/xml")]
        [SwaggerResponse(201, "Пользователь создан")]
        [SwaggerResponse(400, "Некорректные входные данные")]
        [SwaggerResponse(422, "Ошибка при проверке")]
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

        /// <summary>
        /// Обновить пользователя
        /// </summary>
        /// <param name="userId">Идентификатор пользователя</param>
        /// <param name="user">Обновленные данные пользователя</param>
        [HttpPut("{userId}")]
        [Consumes("application/json")]
        [Produces("application/json", "application/xml")]
        [SwaggerResponse(201, "Пользователь создан")]
        [SwaggerResponse(204, "Пользователь обновлен")]
        [SwaggerResponse(400, "Некорректные входные данные")]
        [SwaggerResponse(422, "Ошибка при проверке")]
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

        /// <summary>
        /// Частично обновить пользователя
        /// </summary>
        /// <param name="userId">Идентификатор пользователя</param>
        /// <param name="patchDoc">JSON Patch для пользователя</param>
        [HttpPatch("{userId}")]
        [Consumes("application/json-patch+json")]
        [Produces("application/json", "application/xml")]
        [SwaggerResponse(204, "Пользователь обновлен")]
        [SwaggerResponse(400, "Некорректные входные данные")]
        [SwaggerResponse(404, "Пользователь не найден")]
        [SwaggerResponse(422, "Ошибка при проверке")]
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

        /// <summary>
        /// Удалить пользователя
        /// </summary>
        /// <param name="userId">Идентификатор пользователя</param>
        [HttpDelete("{userId}")]
        [Produces("application/json", "application/xml")]
        [SwaggerResponse(204, "Пользователь удален")]
        [SwaggerResponse(404, "Пользователь не найден")]
        public IActionResult DeleteUser([FromRoute] Guid userId)
        {
            if (userRepository.FindById(userId) == null)
                return NotFound();
            
            userRepository.Delete(userId);
            return NoContent();
        }
        
        /// <summary>
        /// Получить пользователей
        /// </summary>
        /// <param name="pageNumber">Номер страницы, по умолчанию 1</param>
        /// <param name="pageSize">Размер страницы, по умолчанию 20</param>
        /// <response code="200">OK</response>
        [HttpGet(Name = nameof(GetUsers))]
        [Produces("application/json", "application/xml")]
        [ProducesResponseType(typeof(IEnumerable<UserDto>), 200)]
        public IActionResult GetUsers([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 1;
            if (pageSize > 20) pageSize = 20;
            
            var pageList = userRepository.GetPage(pageNumber, pageSize);
            var users = mapper.Map<IEnumerable<UserDto>>(pageList);
            var previousPageNumber = pageNumber - 1;
            var nextPageNumber = pageNumber + 1;
            var previousPage = pageList.HasPrevious
                ? linkGenerator.GetUriByRouteValues(HttpContext, nameof(GetUsers), new {previousPageNumber, pageSize})
                : null;
            var nextPage = pageList.HasNext
                ? linkGenerator.GetUriByRouteValues(HttpContext, nameof(GetUsers), new {nextPageNumber, pageSize})
                : null;
            
            var paginationHeader = new
            {
                previousPageLink = previousPage,
                nextPageLink = nextPage,
                totalCount = pageList.TotalCount,
                pageSize = pageList.PageSize,
                currentPage = pageList.CurrentPage,
                totalPages = pageList.TotalPages,
            };
            Response.Headers.Add("X-Pagination", JsonConvert.SerializeObject(paginationHeader));
            
            return Ok(users);
        }
        
        /// <summary>
        /// Опции по запросам о пользователях
        /// </summary>
        [HttpOptions]
        [SwaggerResponse(200, "OK")]
        public IActionResult GetUsersOptions()
        {
            Response.Headers.Add("Allow", "GET, POST, OPTIONS");
            return Ok();
        }
    }
}