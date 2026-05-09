using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MediaFiles
{
    [TestFixture]
    public class AudioTagEditServiceFixture : CoreTest<AudioTagEditService>
    {
        private BookFile _bookFile;
        private AudioTag _currentTags;
        private AudioTag _suggestedTags;

        [SetUp]
        public void Setup()
        {
            var author = new Author { Id = 7, Name = "Read Author" };
            var book = new Book { Id = 8, Title = "Read Book", Author = author };
            var edition = new Edition { Id = 9, Title = "Read Edition", Book = book };

            _bookFile = new BookFile
            {
                Id = 1,
                EditionId = edition.Id,
                Path = "/books/read-book/read-book.mp3",
                Author = author,
                Edition = edition
            };

            edition.BookFiles = new List<BookFile> { _bookFile };

            _currentTags = new AudioTag
            {
                Title = "Old Title",
                Book = "Old Book",
                BookAuthors = new[] { "Old Author" },
                Performers = new[] { "Old Narrator" },
                Track = 1,
                TrackCount = 1,
                Genres = new[] { "Audiobook" },
                Comment = "Old comment"
            };

            _suggestedTags = new AudioTag
            {
                Title = "Read Edition",
                Book = "Read Book",
                BookAuthors = new[] { "Read Author" },
                Performers = new[] { "Read Narrator" },
                Track = 2,
                TrackCount = 3,
                Genres = new[] { "Audiobook" },
                Comment = "Suggested comment"
            };

            Mocker.GetMock<IMediaFileService>()
                .Setup(x => x.Get(_bookFile.Id))
                .Returns(_bookFile);

            Mocker.GetMock<IMediaFileService>()
                .Setup(x => x.Get(It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { _bookFile.Id }))))
                .Returns(new List<BookFile> { _bookFile });

            Mocker.GetMock<IAudioTagService>()
                .Setup(x => x.ReadAudioTag(_bookFile.Path))
                .Returns(_currentTags);

            Mocker.GetMock<IAudioTagService>()
                .Setup(x => x.GetTrackMetadata(_bookFile))
                .Returns(_suggestedTags);

            Mocker.GetMock<IContributorEvidenceRepository>()
                .Setup(x => x.GetByBookFileIds(It.IsAny<IEnumerable<int>>()))
                .Returns(new List<ContributorEvidence>());

            Mocker.GetMock<IContributorEvidenceRepository>()
                .Setup(x => x.GetByEditionIds(It.IsAny<IEnumerable<int>>()))
                .Returns(new List<ContributorEvidence>());
        }

        [Test]
        public void should_return_current_suggested_and_diff_for_default_preview()
        {
            var result = Subject.GetPreview(_bookFile.Id);

            result.Current.Title.Should().Be("Old Title");
            result.Suggested.Title.Should().Be("Read Edition");
            result.Proposed.Title.Should().Be("Read Edition");
            result.Changes.Should().Contain(x => x.Field == "Title" && x.CurrentValue == "Old Title" && x.ProposedValue == "Read Edition");
            result.CanWrite.Should().BeTrue();
        }

        [Test]
        public void should_preview_manual_values_without_writing_file()
        {
            var result = Subject.Preview(_bookFile.Id, new AudioTagValues
            {
                Title = "Manual Title",
                Book = "Manual Book",
                Author = "Manual Author",
                Performers = "Manual Narrator",
                Track = 4,
                TrackCount = 5,
                Genres = "Audio; Spoken",
                Comment = "Manual comment"
            });

            result.Proposed.Title.Should().Be("Manual Title");
            result.Proposed.Performers.Should().Be("Manual Narrator");
            result.Proposed.Genres.Should().Be("Audio; Spoken");
            result.Changes.Should().Contain(x => x.Field == "Comment" && x.ProposedValue == "Manual comment");

            Mocker.GetMock<IAudioTagService>()
                .Verify(x => x.WriteManualTags(It.IsAny<BookFile>(), It.IsAny<AudioTag>()), Times.Never());
        }

        [Test]
        public void should_write_manual_values_when_diff_exists()
        {
            Mocker.GetMock<IAudioTagService>()
                .SetupSequence(x => x.ReadAudioTag(_bookFile.Path))
                .Returns(_currentTags)
                .Returns(new AudioTag
                {
                    Title = "Manual Title",
                    Book = "Old Book",
                    BookAuthors = new[] { "Old Author" },
                    Performers = new[] { "Old Narrator" },
                    Track = 1,
                    TrackCount = 1,
                    Genres = new[] { "Audiobook" },
                    Comment = "Old comment"
                });

            var result = Subject.Write(_bookFile.Id, new AudioTagValues
            {
                Title = "Manual Title",
                Book = "Old Book",
                Author = "Old Author",
                Performers = "Old Narrator",
                Track = 1,
                TrackCount = 1,
                Genres = "Audiobook",
                Comment = "Old comment"
            });

            result.Changes.Should().BeEmpty();
            result.WriteWarnings.Should().BeEmpty();

            Mocker.GetMock<IAudioTagService>()
                .Verify(x => x.WriteManualTags(_bookFile, It.Is<AudioTag>(t => t.Title == "Manual Title")), Times.Once());
        }

        [Test]
        public void should_warn_when_written_values_do_not_persist()
        {
            var result = Subject.Write(_bookFile.Id, new AudioTagValues
            {
                Title = "Manual Title",
                Book = "Old Book",
                Author = "Old Author",
                Performers = "Old Narrator",
                Track = 1,
                TrackCount = 1,
                Genres = "Audiobook",
                Comment = "Old comment"
            });

            result.Changes.Should().Contain(x => x.Field == "Title" && x.ProposedValue == "Manual Title");
            result.WriteWarnings.Should().Contain(x => x.Contains("Title did not persist"));

            ExceptionVerification.ExpectedWarns(1);
        }

        [Test]
        public void should_preview_template_for_selected_book_files_without_writing()
        {
            var result = Subject.PreviewTemplate(new AudioTagTemplateRequest
            {
                BookFileIds = new List<int> { _bookFile.Id },
                Template = "readarr"
            });

            result.Template.Should().Be("readarr");
            result.TotalFiles.Should().Be(1);
            result.ChangedFiles.Should().Be(1);
            result.Files.Single().Proposed.Title.Should().Be("Read Edition");

            Mocker.GetMock<IAudioTagService>()
                .Verify(x => x.WriteManualTags(It.IsAny<BookFile>(), It.IsAny<AudioTag>()), Times.Never());
        }

        [Test]
        public void should_apply_plex_template_with_audiobook_genre_fallback()
        {
            _currentTags.Genres = new string[0];

            var result = Subject.PreviewTemplate(new AudioTagTemplateRequest
            {
                BookFileIds = new List<int> { _bookFile.Id },
                Template = "plexAudiobook"
            });

            var proposed = result.Files.Single().Proposed;
            proposed.Book.Should().Be("Read Book");
            proposed.Author.Should().Be("Read Author");
            proposed.Performers.Should().Be("Read Narrator");
            proposed.Genres.Should().Be("Audiobook");
        }

        [Test]
        public void should_write_template_and_report_remaining_unpersisted_fields()
        {
            var result = Subject.WriteTemplate(new AudioTagTemplateRequest
            {
                BookFileIds = new List<int> { _bookFile.Id },
                Template = "readarr"
            });

            result.TotalFiles.Should().Be(1);
            result.WarningFiles.Should().Be(1);
            result.Warning.Should().Contain("did not fully persist");
            result.Files.Single().WriteWarnings.Should().NotBeEmpty();

            Mocker.GetMock<IAudioTagService>()
                .Verify(x => x.WriteManualTags(_bookFile, It.Is<AudioTag>(t => t.Title == "Read Edition")), Times.Once());

            ExceptionVerification.ExpectedWarns(1);
        }

        [Test]
        public void should_warn_and_preserve_current_track_count_for_incomplete_sets()
        {
            Mocker.GetMock<IMultiFileBookFileCompletenessService>()
                .Setup(x => x.GetIssue(It.IsAny<List<BookFile>>()))
                .Returns(MultiFileBookFileCompletenessIssue.FromMessage("Missing part 2"));

            var result = Subject.GetPreview(_bookFile.Id);

            result.Warning.Should().Contain("incomplete audiobook part set");
            result.Suggested.TrackCount.Should().Be((int)_currentTags.TrackCount);
        }

        [Test]
        public void should_use_manual_narrator_evidence_for_suggested_performer()
        {
            Mocker.GetMock<IContributorEvidenceRepository>()
                .Setup(x => x.GetByBookFileIds(It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { _bookFile.Id }))))
                .Returns(new List<ContributorEvidence>
                {
                    new ContributorEvidence
                    {
                        Id = 1,
                        BookFileId = _bookFile.Id,
                        Role = "narrator",
                        DisplayName = "Manual Evidence Narrator",
                        Source = "manual",
                        Updated = DateTime.UtcNow
                    }
                });

            var result = Subject.GetPreview(_bookFile.Id);

            result.Suggested.Performers.Should().Be("Manual Evidence Narrator");
        }

        [Test]
        public void should_use_high_confidence_review_narrator_evidence_for_suggested_performer()
        {
            Mocker.GetMock<IContributorEvidenceRepository>()
                .Setup(x => x.GetByEditionIds(It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { _bookFile.EditionId }))))
                .Returns(new List<ContributorEvidence>
                {
                    new ContributorEvidence
                    {
                        Id = 2,
                        EditionId = _bookFile.EditionId,
                        Role = "narrator",
                        DisplayName = "Transcript Evidence Narrator",
                        Source = "sttTranscript",
                        Confidence = 91,
                        Updated = DateTime.UtcNow
                    }
                });

            var result = Subject.GetPreview(_bookFile.Id);

            result.Suggested.Performers.Should().Be("Transcript Evidence Narrator");
        }

        [Test]
        public void should_ignore_low_confidence_review_narrator_evidence_for_suggested_performer()
        {
            Mocker.GetMock<IContributorEvidenceRepository>()
                .Setup(x => x.GetByBookFileIds(It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { _bookFile.Id }))))
                .Returns(new List<ContributorEvidence>
                {
                    new ContributorEvidence
                    {
                        Id = 3,
                        BookFileId = _bookFile.Id,
                        Role = "narrator",
                        DisplayName = "Low Confidence Narrator",
                        Source = "aiReview",
                        Confidence = 62,
                        Updated = DateTime.UtcNow
                    }
                });

            var result = Subject.GetPreview(_bookFile.Id);

            result.Suggested.Performers.Should().Be("Read Narrator");
        }
    }
}
