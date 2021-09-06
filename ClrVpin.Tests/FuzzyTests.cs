using System.Diagnostics.CodeAnalysis;
using ClrVpin.Shared;
using NUnit.Framework;
using NUnit.Framework.Internal;

namespace ClrVpin.Tests
{
    public class FuzzyTests
    {
        [SuppressMessage("ReSharper", "StringLiteralTypo")]
        [Test]
        [TestCase("Indiana Jones (Williams 1993) blah.directb2s", "indiana jones", "indianajones", "williams", 1993)]
        [TestCase("Indiana Jones (Williams) blah.directb2s", "indiana jones", "indianajones", "williams", null)]
        [TestCase("Indiana Jones (1993) blah.directb2s", "indiana jones", "indianajones", null, 1993)]
        [TestCase("Indiana Jones.directb2s", "indiana jones", "indianajones", null, null)]
        [TestCase("Indiana Jones (blah) (Williams 1993).directb2s", "indiana jones", "indianajones", "williams", 1993, TestName = "only last most parenthesis is used")]
        [TestCase("", null, null, null, null, TestName = "empty string")]
        [TestCase(null, null, null, null, null, TestName = "null string")]
        [TestCase("123", "123", "123", null, null, TestName = "number title")]
        [TestCase("123 (Williams 1993)", "123", "123", "williams", 1993, TestName = "number title with manufacturer and year")]
        [TestCase("123 (Williams)", "123", "123", "williams", null, TestName = "number title with manufacturer only")]
        [TestCase("123 (1993)", "123", "123", null, 1993, TestName = "number titleand with year only")]
        [TestCase("123 blah (Williams 1993)", "123 blah", "123blah", "williams", 1993, TestName = "number and word title with manufacturer and year")]
        [TestCase("123 blah (1993)", "123 blah", "123blah", null, 1993, TestName = "number title with word and year only")]
        [TestCase("1-2-3 (1971)", "1 2 3", "123", null, 1971, TestName = "dashes removed.. white space and no white space")]
        public void GetFileNameDetailsTest(string fileName, string expectedName, string expectedNameNoWhiteSpace, string expectedManufacturer, int? expectedYear)
        {
            var (name, nameNoWhiteSpace, manufacturer, year) = Fuzzy.GetFileDetails(fileName);

            Assert.That(name, Is.EqualTo(expectedName));
            Assert.That(nameNoWhiteSpace, Is.EqualTo(expectedNameNoWhiteSpace));
            Assert.That(manufacturer, Is.EqualTo(expectedManufacturer));
            Assert.That(year, Is.EqualTo(expectedYear));
        }

        [SuppressMessage("ReSharper", "StringLiteralTypo")]
        [Test]
        [TestCase("medieval madness", "medieval madness", true)]
        [TestCase("medieval madness.vpx", "medieval madness", true, TestName = "remove extension")]
        [TestCase("medieval madness.vpx", "medieval madness", true)]
        [TestCase("medieval madness", "medieval madness.vpx", true)]
        [TestCase("medieval madness", "medieval madness.vpx", true)]
        [TestCase("medieval madness", "medieval madness (Williams 2006)", true)]
        [TestCase("medieval madness (Williams 2006)", "medieval madness", true)]
        [TestCase("medieval madnes (Williams 2006)", "medieval madness", true, TestName="15 char minimum")]
        [TestCase("medieval madness (Williams 2006)", "medieval madness (blah 2006)", true)]
        [TestCase("medieval madness (  Williams 2006)", "medieval madness (blah 2006)", true)]
        [TestCase(" medieval madness (Williams 2006)", "medieval madness (blah 2006)", true, TestName = "trim whitespace")]
        [TestCase("medieval   madness (Williams 2006)", "medieval madness (blah 2006)", true, TestName = "remove x2 whitespace")]
        [TestCase("medieval    madness (Williams 2006)", "medieval madness (blah 2006)", true, TestName = "remove x4 whitespace")]
        [TestCase("medieval     madness (Williams 2006)", "medieval madness (blah 2006)", true, TestName = "remove x5 whitespace")]
        [TestCase("medieval              madness (Williams 2006)", "medieval madness (blah 2000)", false, TestName = "remove lots of whitespace")]
        [TestCase("medieval madnesas (Williams 2006)", "medieval madness", false, TestName = "typo")]
        [TestCase("ali (Stern 1980)", "ali", true, TestName = "short name exact match")]
        [TestCase("ali (Williams 2006)", "alien (blah)", false, TestName = "#1 - minimum 15 characters required for partial match")]
        [TestCase("black knight 2000", "black knight", false, TestName = "#2 - minimum 15 characters required for partial match")]
        [TestCase("black knight returns 2000", "black knight", false, TestName = "#3 - minimum 15 characters required for partial match")]
        [TestCase("black knight returns 2000", "black knight retur", true, TestName = "#4 - minimum 15 characters required for partial match")]
        [TestCase("the black knight", "black knight", true, TestName = "remove 'the'")]
        [TestCase("black&apos; knight", "black knight", true, TestName = "remove '&apos;'")]
        [TestCase("black' knight", "black knight", true, TestName = "remove '''")]
        [TestCase("black` knight", "black knight", true, TestName = "remove '`'")]
        [TestCase("black, knight", "black knight", true, TestName = "remove ','")]
        [TestCase("black; knight", "black knight", true, TestName = "remove ';'")]
        [TestCase("black knight!", "black knight", true, TestName = "remove '!'")]
        [TestCase("black? knight", "black knight", true, TestName = "remove '?'")]
        [TestCase("black.knight.blah", "black knight", true, TestName = "replace '.'")]
        [TestCase("black-knight", "black knight", true, TestName = "remove '-'")]
        [TestCase("black - knight", "black knight", true, TestName = "remove ' - '")]
        [TestCase("black_knight", "black knight", true, TestName = "remove '_'")]
        [TestCase("black&knight", "black and knight", true, TestName = "replace '&'")]
        [TestCase("black & knight", "black and knight", true, TestName = "replace ' & '")]
        [TestCase("Rocky and Bullwinkle And Friends (Data East 1993)", "Adventures of Rocky and Bullwinkle and Friends (1993).directb2s", true, TestName = "#1 contains - 20 characters satisified")]
        [TestCase("Rocky and Bull", "Adventures of Rocky and Bullwinkle and Friends (1993).directb2s", false, TestName = "#1 contains - characters not satisified")]
        [TestCase("Indiana Jones (Stern 2008)", @"C:\temp\_download\vp\Backglasses\Indiana Jones (Stern 2008) by Starlion.directb2s", true, TestName = "full path")]
        [TestCase("Indiana Jones The Pinball Adventure (1993).directb2s", @"Indiana Jones The Pinball Adventure (Williams 1993).directb2s", true, TestName = "misc")]
        [TestCase("The Getaway High Speed II (Williams 1992)", @"C:\temp\_MegaSync\b2s\Getaway, The - High Speed II v1.04.directb2s", true, TestName = "full path 2")]
        [TestCase("The Getaway High Speed 2 (Williams 1992)", @"C:\temp\_MegaSync\b2s\Getaway, The - High Speed II v1.04.directb2s", true, TestName = "roman numeral conversion - II")]
        [TestCase("The Getaway High Speed 3 (Williams 1992)", @"C:\temp\_MegaSync\b2s\Getaway, The - High Speed III v1.04.directb2s", true, TestName = "roman numeral conversion - III")]
        [TestCase("The Getaway High Speed 4 (Williams 1992)", @"C:\temp\_MegaSync\b2s\Getaway, The - High Speed IV v1.04.directb2s", true, TestName = "roman numeral conversion - IV")]
        [TestCase("Lights...Camera...Action! (Premier 1989).blah", @"Lights Camera Action (1989).directb2s", true, TestName = "ellipsis")]
        [TestCase("Lights...Camera...Action! (Premier 1989)", @"Lights Camera Action (1989).directb2s", false, TestName = "ellipsis - without file extension not supported :(")]
        [TestCase("1-2-3 (Premier 1989)", "123 (Premier1989)", true, TestName = "#1 white space - removed")]
        [TestCase("123 (Premier 1989)", "1 2 3 (Premier1989)", true, TestName = "#1 white space - removed 2")]
        [TestCase("1 2 3 (Premier 1989)", "1-2-3-(Premier1989)", true, TestName = "#1 white space - removed 3")]
        [TestCase("1 2   3 (Premier 1989)", "1-2-3-(Premier1989)", true, TestName = "#1 white space - removed 4")]
        [TestCase("1-2-3 (Premier 1989)", "1 2 3 (Premier1989)", true, TestName = "#1 white space - kept")]
        [TestCase("AC-DC LUCI Premium (Stern 2013).directb2s", "AC-DC LUCI (Stern 2013).directb2s", true, TestName = "remove 'premium'")]
        [TestCase("Amazon Hunt baby baby VPX 1.6.directb2s", "Amazon Hunt baby baby (1983).directb2s", true, TestName = "remove 'vpx'")]
        public void MatchTest(string first, string second, bool expectedSuccess)
        {
            var isMatch = Fuzzy.Match(first, Fuzzy.GetFileDetails(second)).success;

            Assert.That(isMatch, Is.EqualTo(expectedSuccess));
        }

        [Test]
        [TestCase("Indiana Jones Rocks (Stern 1993)", "Indiana Jones Rocks (Stern)", true, 150, TestName = "exact name and missing year")]
        [TestCase("Indiana Jones Rocks (Stern 1993)", "Indiana Jones Rocks (Stern 1993)", true, 200, TestName = "exact name and exact year")]
        [TestCase("Indiana Jones Rocks (Stern 1993)", "Indiana Jones Rocks (Stern 1994)", true, 190, TestName = "exact name and +/-1 year")]
        [TestCase("Indiana Jones Rocks (Stern 1993)", "Indiana Jones Rocks (Stern 1995)", true, 100, TestName = "exact name and +/-2 year")]
        [TestCase("Indiana Jones Rocks (Stern 1993)", "Indiana Jones Rocks (Stern 1996)", false, 50, TestName = "exact name and +/-3 year")]
        [TestCase("Indiana Jones Rocks (Stern 1993)", "Indiana Jones Rocks (Stern 1997)", false, -850, TestName = "exact name and +/-3 year")]
        [TestCase("Indiana Jones Rocks (Stern 1993)", "Indiana Jones Rocks Baby (Stern)", true, 100, TestName = "starts name 15char and missing year")]
        [TestCase("Indiana Jones Rocks (Stern 1993)", "Indiana Jones Rocks Baby (Stern 1993)", true, 150, TestName = "starts name 15char and exact year")]
        [TestCase("Indiana Jones Rocks (Stern 1993)", "Indiana Jones Rocks Baby (Stern 1994)", true, 140, TestName = "starts name 15char and +/-1 year")]
        [TestCase("Indiana Jones Rocks (Stern 1993)", "Indiana Jones Rocks Baby (Stern 1995)", false, 50, TestName = "starts name 15char and +/-2 year")]
        [TestCase("Indiana Jones (Stern 1993)", "Indiana Jones Rocks (Stern)", false, 60, TestName = "starts name 10char and missing year")]
        [TestCase("Indiana Jones (Stern 1993)", "Indiana Jones Rocks (Stern 1993)", true, 110, TestName = "starts name 10char and exact year")]
        [TestCase("Indiana Jones (Stern 1993)", "Indiana Jones Rocks (Stern 1992)", true, 100, TestName = "starts name 10char and +/-1 year")]
        [TestCase("Indiana Jones (Stern 1993)", "Indiana Jones Rocks (Stern 1991)", false, 10, TestName = "starts name 10char and +/-1 year")]
        [TestCase("Indiana Jones Rocks Baby (Stern 1993)", "OMG Indiana Jones Rocks Baby (Stern)", true, 100, TestName = "contains name 20char and missing year")]
        [TestCase("Indiana Jones Rocks Baby (Stern 1993)", "OMG Indiana Jones Rocks Baby (Stern 1993)", true, 150, TestName = "contains name 20char and exact year")]
        [TestCase("Indiana Jones Rocks Baby (Stern 1993)", "OMG Indiana Jones Rocks Baby (Stern 1994)", true, 140, TestName = "contains name 20char and +/-1 year")]
        [TestCase("Indiana Jones R (Stern 1993)", "OMG Indiana Jones Rocks (Stern)", false, 60, TestName = "contains name 13char and missing year")]
        [TestCase("Indiana Jones R (Stern 1993)", "OMG Indiana Jones Rocks (Stern 1993)", true, 110, TestName = "contains name 13char and exact year")]
        [TestCase("Indiana Jones R (Stern 1993)", "OMG Indiana Jones Rocks (Stern 1994)", true, 100, TestName = "contains name 13char and +/-1 year")]
        [TestCase("Indiana Jones R (Stern 1993)", "OMG Indiana Jones Rocks (Stern 1995)", false, 10, TestName = "contains name 13char and +/-2 year")]
        public void MatchScoreTest(string first, string second, bool expectedSuccess, int expectedScore)
        {
            var (success, score) = Fuzzy.Match(first, Fuzzy.GetFileDetails(second));

            Assert.That(success, Is.EqualTo(expectedSuccess));
            Assert.That(score, Is.EqualTo(expectedScore));
        }
    }
}