﻿using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MyVote.Server.Dtos;
using MyVote.Server.Hubs;
using MyVote.Server.Models;
using System.Linq;
using System.Xml.Serialization;

namespace MyVote.Server.Controllers
{
    [ApiController]
    [Route("/api")]
    [EnableCors("AllowSpecificOrigin")]
    public class MyVoteController : ControllerBase
    {
        private readonly ILogger<MyVoteController> _logger;
        private readonly AppDbContext _db;

        public MyVoteController(ILogger<MyVoteController> logger, AppDbContext dbContext)
        {
            _logger = logger;
            _db = dbContext;
        }

        //Get user if already existing, else create new one

        [HttpGet("track")]
        public IActionResult TrackUser()
        {
            var existingCookie = Request.Cookies["user_id"];

            if (!string.IsNullOrEmpty(existingCookie))
            {
                var existingUser = _db.Users.FirstOrDefault(u => u.LastName == existingCookie);

                if (existingUser != null)
                {
                    return Ok(new { message = "Existing user found", userId = existingUser.UserId });
                }
            }

            string newUserCookie = Guid.NewGuid().ToString();

            var newUser = new User
            {
                FirstName = "Guest",
                LastName = newUserCookie,
            };

            _db.Users.Add(newUser);
            _db.SaveChanges();


            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Expires = DateTime.UtcNow.AddYears(1)
            };

            Response.Cookies.Append("user_id", newUserCookie, cookieOptions);

            return Ok(new { message = "New user tracked", userId = newUser.UserId });
        }

        //Get user by id
        [HttpGet("user/{userid}")]
        public async Task<ActionResult<UserDto>> GetUser(int userid)
        {
            var user = await _db.Users.FindAsync(userid);
            if (user == null)
                return NotFound();

            var userDto = new UserDto
            {
                UserId = user.UserId,
                FirstName = user.FirstName,
                LastName = user.LastName
            };

            return Ok(userDto);
        }

        //Create new user
        [HttpPost("user")]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserDto newUserDto)
        {
            var user = new User
            {
                FirstName = newUserDto.FirstName,
                LastName = newUserDto.LastName
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetUser), new { userid = user.UserId }, newUserDto);
        }

        //Get all polls belonging to user
        [HttpGet("polls/{userId}")]
        public async Task<ActionResult<IEnumerable<Poll>>> GetPollsByUser(int userId)
        {
            // Polls where the user has voted (via UserChoices)
            var votedPolls = await _db.UserChoices
                .Where(uc => uc.UserId == userId)
                .Select(uc => uc.Choice.Poll)
                .Distinct()
                .ToListAsync();

            // Polls created by the user
            var createdPolls = await _db.Polls
                .Where(p => p.UserId == userId)
                .ToListAsync();

            // Combine and remove duplicates
            var allPolls = votedPolls.Concat(createdPolls).Distinct().ToList();

            if (!allPolls.Any())
            {
                return NotFound(new { message = "No polls found for this user." });
            }

            return Ok(allPolls);
        }

        //Get poll user voted on
        [HttpGet("polls/voted/{userId}")]
        public async Task<ActionResult<IEnumerable<Poll>>> GetVotedPolls(int userId)
        {
            var votedPolls = await _db.UserChoices
                .Where(uc => uc.UserId == userId)
                .Select(uc => uc.Choice.Poll)
                .Distinct()
                .ToListAsync();

            foreach (Poll poll in votedPolls)
            {
                await UpdateStatus(poll);
            }

            if (!votedPolls.Any())
            {
                return Ok(new List<Poll>());
            }

            return Ok(votedPolls);
        }

        //Get polls user has created
        [HttpGet("polls/owned/{userId}")]
        public async Task<ActionResult<IEnumerable<Poll>>> GetOwnedPolls(int userId)
        {
            var createdPolls = await _db.Polls
                .Where(p => p.UserId == userId)
                .ToListAsync();

            foreach (Poll poll in createdPolls)
            {
                await UpdateStatus(poll);
            }

            if (!createdPolls.Any())
            {
                return Ok(new List<Poll>());
            }

            return Ok(createdPolls);
        }

        //Get poll details
        [HttpGet("poll/{pollid}")]
        public async Task<ActionResult<PollDto>> GetPoll(int pollid)
        {
            var poll = await _db.Polls
                .Include(p => p.Choices)
                    .ThenInclude(c => c.UserChoices) // Include UserChoices to retrieve UserId
                .FirstOrDefaultAsync(p => p.PollId == pollid);

            if (poll == null)
            {
                return NotFound();
            }

            await UpdateStatus(poll);

            var pollDto = new PollDto
            {
                PollId = poll.PollId,
                Title = poll.Title,
                Description = poll.Description,
                DateCreated = poll.DateCreated,
                DateEnded = poll.DateEnded,
                PollType = poll.PollType,
                IsActive = poll.IsActive,
                UserId = poll.UserId, // Ensure the UserId of the poll creator is included
                Choices = poll.Choices.Select(c => new ChoiceDto
                {
                    ChoiceId = c.ChoiceId,
                    Name = c.Name,
                    NumVotes = c.NumVotes,
                    UserIds = c.UserChoices.Select(uc => uc.UserId).ToList()
                }).ToList()
            };

            return Ok(pollDto);
        }

        //Update status of poll to inactive
        [HttpPatch("poll/status")]
        public async Task<IActionResult> UpdateStatus([FromBody] Poll poll)
        {
            if (DateTime.UtcNow >= poll.DateEnded && poll.IsActive == "t")
                if (DateTime.UtcNow >= poll.DateEnded && poll.IsActive == "t")
                {
                    poll.IsActive = "f";
                }
            _db.SaveChangesAsync();
            return Ok();
        }

        //End a poll
        [HttpPatch("poll/{pollId}/end")]
        public async Task<IActionResult> EndPoll(int pollId)
        {
            var poll = await _db.Polls
                .Include(p => p.Choices)
                    .ThenInclude(c => c.UserChoices) // Include UserChoices to retrieve UserId
                .FirstOrDefaultAsync(p => p.PollId == pollId);

            if (poll == null)
            {
                return NotFound(new { message = "Poll not found." });
            }

            poll.DateEnded = DateTime.UtcNow;
            poll.IsActive = "f";
            await _db.SaveChangesAsync();

            var pollDto = new PollDto
            {
                PollId = poll.PollId,
                Title = poll.Title,
                Description = poll.Description,
                DateCreated = poll.DateCreated,
                DateEnded = poll.DateEnded,
                IsActive = poll.IsActive,
                PollType = poll.PollType,
                UserId = poll.UserId, // Ensure the UserId of the poll creator is included
                Choices = poll.Choices.Select(c => new ChoiceDto
                {
                    ChoiceId = c.ChoiceId,
                    Name = c.Name,
                    NumVotes = c.NumVotes,
                    UserIds = c.UserChoices.Select(uc => uc.UserId).ToList()
                }).ToList()
            };

            var hubContext = HttpContext.RequestServices.GetRequiredService<IHubContext<GlobalHub>>();
            try
            {
                await hubContext.Clients.All.SendAsync("EndedPoll", pollDto);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            return Ok(pollDto);
        }

        [HttpPatch("poll/suggestion")]
        public async Task<IActionResult> UpdatePoll([FromBody] OptionDto optionDto)
        {
            // Find the poll with choices and user choices, including UserChoices for each Choice
            var poll = await _db.Polls
                .Include(p => p.Choices) // Include Choices
                .ThenInclude(c => c.UserChoices) // Eagerly load UserChoices for each Choice
                .FirstOrDefaultAsync(p => p.PollId == optionDto.PollId);

            if (poll == null)
            {
                return NotFound(new { message = "Poll not found" });
            }

            if (poll.IsActive == "f")
            {
                return StatusCode(410, new { message = "Poll is no longer active." });
            }

            // Create a new choice using the suggestion
            var newChoice = new Choice
            {
                Name = optionDto.SuggestionName,
                PollId = optionDto.PollId,
                NumVotes = 0
            };

            // Add to poll
            poll.Choices.Add(newChoice);

            // Save to database
            await _db.SaveChangesAsync();

            // Construct the updated PollDto with UserIds
            var updatedPollDto = new PollDto
            {
                UserId = poll.UserId,
                PollId = poll.PollId,
                Title = poll.Title,
                Description = poll.Description,
                DateCreated = poll.DateCreated,
                DateEnded = poll.DateEnded,
                PollType = poll.PollType,
                IsActive = poll.IsActive,
                Choices = poll.Choices.Select(c => new ChoiceDto
                {
                    ChoiceId = c.ChoiceId,
                    Name = c.Name,
                    NumVotes = c.NumVotes,
                    UserIds = c.UserChoices.Select(uc => uc.UserId).ToList() // Ensure UserIds are populated
                }).ToList()
            };

            // Broadcast the updated poll
            var hubContext = HttpContext.RequestServices.GetRequiredService<IHubContext<GlobalHub>>();
            try
            {
                await hubContext.Clients.All.SendAsync("UpdatedPoll", updatedPollDto);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            return Ok(new { message = "Choice added successfully", choiceId = newChoice.ChoiceId });
        }

        [HttpPatch("poll/survey/opinion")]
        public async Task<IActionResult> AddOpinion([FromBody] OpinionDto opinionDto)
        {
            // Find the poll with choices and user choices, including UserChoices for each Choice
            var poll = await _db.Polls
                .Include(p => p.Choices) // Include Choices
                .ThenInclude(c => c.UserChoices) // Eagerly load UserChoices for each Choice
                .FirstOrDefaultAsync(p => p.PollId == opinionDto.PollId);

            if (poll == null)
            {
                return NotFound(new { message = "Poll not found" });
            }

            if (poll.IsActive == "f")
            {
                return StatusCode(410, new { message = "Poll is no longer active." });
            }

            // Create a new choice using the suggestion
            var newChoice = new Choice
            {
                Name = opinionDto.OpinionName,
                PollId = opinionDto.PollId,
                NumVotes = 0
            };

            // Add to poll
            poll.Choices.Add(newChoice);

            // Save to database
            await _db.SaveChangesAsync();

            // Construct the updated PollDto with UserIds
            var updatedPollDto = new PollDto
            {
                UserId = poll.UserId,
                PollId = poll.PollId,
                Title = poll.Title,
                Description = poll.Description,
                DateCreated = poll.DateCreated,
                DateEnded = poll.DateEnded,
                PollType = poll.PollType,
                IsActive = poll.IsActive,
                Choices = poll.Choices.Select(c => new ChoiceDto
                {
                    ChoiceId = c.ChoiceId,
                    Name = c.Name,
                    NumVotes = c.NumVotes,
                    UserIds = c.UserChoices.Select(uc => uc.UserId).ToList() // Ensure UserIds are populated
                }).ToList()
            };

            // Broadcast the updated poll
            var hubContext = HttpContext.RequestServices.GetRequiredService<IHubContext<VoteHub>>();
            try
            {
                await hubContext.Clients.All.SendAsync("ReceivedOpinion", updatedPollDto);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            return Ok(new { message = "Choice added successfully", choiceId = newChoice.ChoiceId });
        }


        [HttpPatch("poll/vote/remove")]
        public async Task<IActionResult> RemoveChoice([FromBody] VoteDto voteDto)
        {
            var choice = await _db.Choices
                .Include(c => c.UserChoices) // Include UserChoices for removal
                .Include(c => c.Poll)
                .FirstOrDefaultAsync(c => c.ChoiceId == voteDto.ChoiceId);

            if (choice == null) return NotFound("Choice not found.");

            var userChoice = choice.UserChoices.FirstOrDefault(uc => uc.UserId == voteDto.UserId);
            if (userChoice == null) return BadRequest("User has not voted for this choice.");

            _db.UserChoices.Remove(userChoice); // Remove vote
            choice.NumVotes--; // Decrease vote count

            await _db.SaveChangesAsync();

            // Get updated poll data
            var updatedPoll = await _db.Polls
                .Include(p => p.Choices)
                .ThenInclude(c => c.UserChoices) // Include votes
                .FirstOrDefaultAsync(p => p.PollId == choice.PollId);

            var updatedPollDto = new PollDto
            {
                UserId = updatedPoll.UserId,
                PollId = updatedPoll.PollId,
                Title = updatedPoll.Title,
                Description = updatedPoll.Description,
                DateCreated = updatedPoll.DateCreated,
                DateEnded = updatedPoll.DateEnded,
                PollType = updatedPoll.PollType,
                IsActive = updatedPoll.IsActive,
                Choices = updatedPoll.Choices.Select(c => new ChoiceDto
                {
                    ChoiceId = c.ChoiceId,
                    Name = c.Name,
                    NumVotes = c.NumVotes,
                    UserIds = c.UserChoices.Select(uc => uc.UserId).ToList()
                }).ToList()
            };

            // Broadcast the update via SignalR
            var hubContext = HttpContext.RequestServices.GetRequiredService<IHubContext<VoteHub>>();
            await hubContext.Clients.All.SendAsync("RemoveVoteUpdate", updatedPollDto);

            return Ok(new { message = "Vote removed successfully!" });
        }


        [HttpPatch("poll/vote")]
        public async Task<IActionResult> UpdateChoice([FromBody] VoteDto voteDto)
        {
            var choice = await _db.Choices
                .Include(c => c.Poll)
                .Include(c => c.UserChoices) // Include UserChoices to check for existing votes
                .FirstOrDefaultAsync(c => c.ChoiceId == voteDto.ChoiceId);

            if (choice == null) return NotFound("Choice not found.");

            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == voteDto.UserId);
            if (user == null) return NotFound("User not found.");

            // Check if the user has already voted for this choice
            var existingUserChoice = choice.UserChoices.FirstOrDefault(uc => uc.UserId == voteDto.UserId);

            // If the user hasn't voted for this choice, add their vote
            if (existingUserChoice == null)
            {
                var userChoice = new UserChoice
                {
                    UserId = voteDto.UserId,
                    ChoiceId = voteDto.ChoiceId
                };

                _db.UserChoices.Add(userChoice);
                choice.NumVotes++;
            }

            await _db.SaveChangesAsync();

            // Get updated poll data
            var updatedPoll = await _db.Polls
                .Include(p => p.Choices)
                .ThenInclude(c => c.UserChoices) // Include user choices
                .FirstOrDefaultAsync(p => p.PollId == choice.PollId);

            var updatedPollDto = new PollDto
            {
                UserId = updatedPoll.UserId,
                PollId = updatedPoll.PollId,
                Title = updatedPoll.Title,
                Description = updatedPoll.Description,
                DateCreated = updatedPoll.DateCreated,
                DateEnded = updatedPoll.DateEnded,
                PollType = updatedPoll.PollType,
                IsActive = updatedPoll.IsActive,
                Choices = updatedPoll.Choices.Select(c => new ChoiceDto
                {
                    ChoiceId = c.ChoiceId,
                    Name = c.Name,
                    NumVotes = c.NumVotes,
                    UserIds = c.UserChoices.Select(uc => uc.UserId).ToList()
                }).ToList()
            };

            // Broadcast the updated poll using SignalR
            var hubContext = HttpContext.RequestServices.GetRequiredService<IHubContext<VoteHub>>();
            try
            {
                await hubContext.Clients.All.SendAsync("ReceiveVoteUpdate", updatedPollDto);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            return Ok(new { message = "Vote submitted successfully!" });
        }


        //Create a poll
        [HttpPost("poll")]
        public async Task<IActionResult> CreatePoll([FromBody] CreatePollDto newPollDto)
        {
            // Check if the UserId exists in the Users table
            var user = await _db.Users.FindAsync(newPollDto.UserId);
            if (user == null)
            {
                return BadRequest("Invalid UserId");
            }

            var poll = new Poll
            {
                UserId = newPollDto.UserId,
                Title = newPollDto.Title,
                Description = newPollDto.Description,
                DateCreated = newPollDto.DateCreated,
                DateEnded = newPollDto.DateEnded,
                PollType = newPollDto.PollType,
                IsActive = newPollDto.IsActive,
                Choices = newPollDto.Choices.Select(c => new Choice
                {
                    Name = c.Name,
                    NumVotes = c.NumVotes
                }).ToList()
            };

            _db.Polls.Add(poll);
            await _db.SaveChangesAsync();

            var pollDto = new PollDto
            {
                PollId = poll.PollId,
                Title = poll.Title,
                Description = poll.Description,
                DateCreated = poll.DateCreated,
                DateEnded = poll.DateEnded,
                PollType = poll.PollType,
                IsActive = poll.IsActive,
                Choices = poll.Choices.Select(c => new ChoiceDto
                {
                    ChoiceId = c.ChoiceId,
                    Name = c.Name,
                    NumVotes = c.NumVotes
                }).ToList()
            };

            return CreatedAtAction(nameof(GetPoll), new { pollid = poll.PollId }, pollDto);
        }

        //Delete a poll
        [HttpDelete("poll/{pollid}")]
        public async Task<IActionResult> DeletePoll(int pollid)
        {
            var poll = await _db.Polls
                .Include(p => p.Choices)
                    .ThenInclude(c => c.UserChoices) // Include UserChoices instead of Users
                .FirstOrDefaultAsync(p => p.PollId == pollid);

            if (poll == null)
                return NotFound();

            // Remove all UserChoices associated with the poll's choices
            var userChoicesToRemove = _db.UserChoices.Where(uc => poll.Choices.Select(c => c.ChoiceId).Contains(uc.ChoiceId));
            _db.UserChoices.RemoveRange(userChoicesToRemove);

            // Remove related Choices
            _db.Choices.RemoveRange(poll.Choices);

            // Remove the Poll
            _db.Polls.Remove(poll);

            await _db.SaveChangesAsync();
            return NoContent();
        }

        //Delete a poll only for a specific user
        [HttpDelete("{pollId}/user/{userId}")]
        public async Task<IActionResult> RemoveUserFromPoll(int pollId, int userId)
        {
            var userChoices = await _db.UserChoices
                .Where(uc => uc.Choice.PollId == pollId && uc.UserId == userId)
                .ToListAsync();

            if (!userChoices.Any())
            {
                return NotFound("User has not voted on this poll.");
            }

            _db.UserChoices.RemoveRange(userChoices);
            await _db.SaveChangesAsync();

            return NoContent();
        }

        //Get poll choices
        [HttpGet("choices/{pollid}")]
        public async Task<ActionResult<IEnumerable<ChoiceDto>>> GetPollChoices(int pollid)
        {
            var choices = await _db.Choices
                .Where(c => c.PollId == pollid)
                .ToListAsync();

            if (!choices.Any()) return NotFound();

            var choiceDtos = choices.Select(c => new ChoiceDto
            {
                ChoiceId = c.ChoiceId,
                Name = c.Name,
                NumVotes = c.NumVotes
            }).ToList();

            return Ok(choiceDtos);
        }

        //Get suggestions for a user
        [HttpGet("suggestions/{userId}")]
        public async Task<IActionResult> GetSuggestions(int userId)
        {
            var suggestions = await _db.Suggestions
                .Where(s => s.UserId == userId)
                .ToListAsync();

            return Ok(suggestions);

        }

        //Post a suggestion
        [HttpPost("suggestion")]
        public async Task<IActionResult> SendOption([FromBody] OptionDto optionDto)
        {
            var suggestion = new Suggestion
            {
                SuggestionName = optionDto.SuggestionName,
                PollId = optionDto.PollId,
                UserId = optionDto.UserId,
                PollName = optionDto.PollName,
            };

            _db.Suggestions.Add(suggestion);

            await _db.SaveChangesAsync();

            var updatedOptionDto = new OptionDto
            {
                SuggestionId = suggestion.SuggestionId, // Newly created ID
                SuggestionName = suggestion.SuggestionName,
                PollId = suggestion.PollId,
                UserId = suggestion.UserId,
                PollName = suggestion.PollName
            };

            var hubContext = HttpContext.RequestServices.GetRequiredService<IHubContext<GlobalHub>>();
            try
            {
                await hubContext.Clients.All.SendAsync("ReceiveWriteInOption", updatedOptionDto);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            return Ok(new { message = $"Option submitted to {optionDto.UserId}" });
        }

        //Delete a suggestion
        [HttpDelete("suggestion/{suggestionId}")]
        public async Task<IActionResult> DeleteSuggestion(int suggestionId)
        {
            var suggestion = await _db.Suggestions.FirstOrDefaultAsync(s => s.SuggestionId == suggestionId);

            if (suggestion == null)
            {
                return NotFound(new { message = "Suggestion not found" });
            }

            _db.Suggestions.Remove(suggestion);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Suggestion deleted successfully" });
        }
    }
}