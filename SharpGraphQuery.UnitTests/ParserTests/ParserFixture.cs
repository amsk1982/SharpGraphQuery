using System;
using System.IO;
using System.Reflection;
using FluentAssertions;
using SharpGraphQl;
using Xunit;

namespace SharpGraphQuery.UnitTests.ParserTests
{
    public abstract class ParserFixture
    {
        public ParserFixture()
        {
            Type t = GetType();
            Assembly a = t.Assembly;
            string resourceName = t.Name + ".graphql";
            Stream stream = a.GetManifestResourceStream(t, resourceName);
            if (stream == null)
                throw new InvalidOperationException("Cannot find " + resourceName);
            string query;
            using (stream)
            using (var rdr = new StreamReader(stream))
            {
                query = rdr.ReadToEnd();
            }

            GraphQueryTokenReader reader = new GraphQueryTokenReader(query);
            Parser = new GraphQueryParser(reader);
        }

        protected GraphQueryParser Parser { get; }

        [Fact]
        public void CanParse()
        {
            var parseResult = Parser.Parse();
            var ast = GetAst();
            parseResult.Should().BeEquivalentTo(ast);
        }

        protected abstract RootNode GetAst();
    }
}