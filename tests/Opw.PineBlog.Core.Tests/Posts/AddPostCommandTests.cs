using FluentAssertions;
using FluentValidation.Results;
using Moq;
using Opw.HttpExceptions;
using Opw.PineBlog.Entities;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Opw.PineBlog.Posts
{
    public class AddPostCommandTests : MediatRTestsBase
    {
        private Guid _authorId = Guid.NewGuid();

        public AddPostCommandTests()
        {
            var author = new Author { Id = _authorId, UserName = "user@example.com", DisplayName = "Author 1" };

            AuthorRepositoryMock.Setup(m => m.SingleOrDefaultAsync(It.IsAny<Expression<Func<Author, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(author);
        }

        [Fact]
        public async Task Validator_Should_ThrowValidationErrorException()
        {
            Task action() => Mediator.Send(new AddPostCommand());

            var ex = await Assert.ThrowsAsync<ValidationErrorException<ValidationFailure>>(action);
            ex.Errors.Single(e => e.Key.Equals(nameof(AddPostCommand.UserName))).Should().NotBeNull();
            ex.Errors.Single(e => e.Key.Equals(nameof(AddPostCommand.Title))).Should().NotBeNull();
            ex.Errors.Single(e => e.Key.Equals(nameof(AddPostCommand.Content))).Should().NotBeNull();
        }

        [Fact]
        public async Task Handler_Should_ReturnNotFoundException_WhenInvalidUser()
        {
            AuthorRepositoryMock.Setup(m => m.SingleOrDefaultAsync(It.IsAny<Expression<Func<Author, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(default(Author));

            var result = await Mediator.Send(new AddPostCommand
            {
                UserName = "invalid@example.com",
                Categories = "category",
                Title = "title",
                Content = "content",
                Description = "description"
            });

            result.IsSuccess.Should().BeFalse();
            result.Exception.Should().BeOfType<NotFoundException>();
        }

        [Fact]
        public async Task Handler_Should_AddPost()
        {
            var published = DateTime.UtcNow;
            var result = await Mediator.Send(new AddPostCommand
            {
                UserName = "user@example.com",
                Categories = "category",
                Title = "title",
                Content = "content",
                Description = "description",
                CoverCaption = "cover caption",
                CoverLink = "cover link",
                Published = published
            });

            result.IsSuccess.Should().BeTrue();
            result.Value.AuthorId.Should().Be(_authorId);
            result.Value.Title.Should().Be("title");
            result.Value.Categories.Should().Be("category");
            result.Value.Content.Should().Be("content");
            result.Value.Description.Should().Be("description");
            result.Value.CoverCaption.Should().Be("cover caption");
            result.Value.CoverLink.Should().Be("cover link");
            result.Value.Published.Should().Be(published);

            PostRepositoryMock.Verify(m => m.Add(It.IsAny<Post>()), Times.Once);
        }

        [Fact]
        public async Task Handler_Should_CoverUrl_ReplaceBaseUrlWithUrlFormat()
        {
            var result = await Mediator.Send(new AddPostCommand
            {
                UserName = "user@example.com",
                Categories = "category",
                Title = "title",
                Content = "content",
                Description = "description",
                CoverUrl = "http://127.0.0.1:10000/devstoreaccount1/pineblog-tests/blog-cover-url"
            });

            result.IsSuccess.Should().BeTrue();

            result.Value.Should().NotBeNull();
            result.Value.CoverUrl.Should().Be("%URL%/blog-cover-url");
        }

        [Fact]
        public async Task Handler_Should_UrlsInContent_ReplaceBaseUrlWithUrlFormat()
        {
            var result = await Mediator.Send(new AddPostCommand
            {
                UserName = "user@example.com",
                Categories = "category",
                Title = "title",
                Content = "content with an url: http://127.0.0.1:10000/devstoreaccount1/pineblog-tests/content-url-1. nice isn't it? And one more: http://127.0.0.1:10000/devstoreaccount1/pineblog-tests/content-url-2!",
                Description = "description",
            });

            result.IsSuccess.Should().BeTrue();

            result.Value.Should().NotBeNull();
            result.Value.Content.Should().Be("content with an url: %URL%/content-url-1. nice isn't it? And one more: %URL%/content-url-2!");
        }

        [Fact]
        public async Task Handler_Should_HaveSlug()
        {
            var result = await Mediator.Send(new AddPostCommand
            {
                UserName = "user@example.com",
                Categories = "category",
                Title = "title or slug",
                Content = "content",
                Description = "description"
            });

            result.IsSuccess.Should().BeTrue();
            result.Value.Title.Should().Be("title or slug");
            result.Value.Slug.Should().MatchRegex(result.Value.Title.ToPostSlug());
        }

        [Fact]
        public async Task Handler_Should_ReturnExceptionResult_WhenSaveChangesError()
        {
            BlogUnitOfWorkMock.Setup(m => m.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Result<int>.Fail(new ApplicationException("Error: SaveChangesAsync")));

            var result = await Mediator.Send(new AddPostCommand
            {
                UserName = "user@example.com",
                Categories = "category",
                Title = "title",
                Content = "content",
                Description = "description"
            });

            result.IsSuccess.Should().BeFalse();
            result.Exception.Should().BeOfType<ApplicationException>();
        }
    }
}
