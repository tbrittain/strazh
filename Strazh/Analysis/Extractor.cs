using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Generic;
using Strazh.Domain;
using System.IO;

namespace Strazh.Analysis
{
    public static class Extractor
    {
        private static TypeNode CreateTypeNode(this ISymbol symbol, TypeDeclarationSyntax declaration)
        {
            (string fullName, string name) = (symbol.ContainingNamespace.ToString() + '.' + symbol.Name, symbol.Name);
            return declaration switch
            {
                ClassDeclarationSyntax _ => new ClassNode(fullName, name, declaration.Modifiers.MapModifiers()),
                InterfaceDeclarationSyntax _ => new InterfaceNode(fullName, name, declaration.Modifiers.MapModifiers()),
                _ => null
            };
        }

        private static ClassNode CreateClassNode(this TypeInfo typeInfo)
            => new ClassNode(GetFullName(typeInfo), GetName(typeInfo));

        private static InterfaceNode CreateInterfaceNode(this TypeInfo typeInfo)
            => new InterfaceNode(GetFullName(typeInfo), GetName(typeInfo));

        private static string[] MapModifiers(this SyntaxTokenList syntaxTokens)
            => syntaxTokens.Select(x => x.ValueText).ToArray();

        private static TypeNode CreateTypeNode(this TypeInfo typeInfo)
        {
            return typeInfo.ConvertedType?.TypeKind switch // TODO: breakpoint when null
            {
                TypeKind.Interface => CreateInterfaceNode(typeInfo),
                TypeKind.Class => CreateClassNode(typeInfo),
                _ => null
            };
        }

        private static string GetName(this TypeInfo typeInfo)
            => typeInfo.Type.Name;

        private static string GetFullName(this TypeInfo typeInfo)
            => typeInfo.Type.ContainingNamespace.ToString() + "." + GetName(typeInfo);

        private static string GetNamespaceName(this INamespaceSymbol namespaceSymbol, string name)
        {
            var nextName = namespaceSymbol?.Name;
            if (string.IsNullOrEmpty(nextName))
            {
                return name;
            }
            return GetNamespaceName(namespaceSymbol.ContainingNamespace, $"{nextName}.{name}");
        }

        private static MethodNode CreateMethodNode(this IMethodSymbol symbol, MethodDeclarationSyntax declaration = null)
        {
            var temp = $"{symbol.ContainingType}.{symbol.Name}";
            var fullName = symbol.ContainingNamespace.GetNamespaceName($"{symbol.ContainingType.Name}.{symbol.Name}");
            var args = symbol.Parameters.Select(x => (name: x.Name, type: x.Type.ToString())).ToArray();
            var returnType = symbol.ReturnType.ToString();
            return new MethodNode(fullName,
                symbol.Name,
                args,
                returnType,
                declaration?.Modifiers.MapModifiers());
        }

        private static string GetName(string filePath)
            => filePath.Split(Path.DirectorySeparatorChar).Reverse().FirstOrDefault();

        private static List<TripleIncludedIn> GetFolderChain(string filePath, FileNode file)
        {
            var triples = new List<TripleIncludedIn>();
            var chain = filePath.Split(Path.DirectorySeparatorChar);
            FolderNode prev = null;
            var path = string.Empty;
            foreach (var item in chain)
            {
                if (string.IsNullOrEmpty(path))
                {
                    path = item;
                    prev = new FolderNode(path, item);
                    continue;
                }
                if (item == file.Name)
                {
                    triples.Add(new TripleIncludedIn(file, prev));
                    return triples;
                }
                else
                {
                    path = Path.DirectorySeparatorChar == '/' ? $"{path}/{item}" : $"{path}\\{item}";
                    triples.Add(new TripleIncludedIn(new FolderNode(path, item), new FolderNode(prev.FullName, prev.Name)));
                    prev = new FolderNode(path, item);
                }
            }
            return triples;
        }

        /// <summary>
        /// Entry to analyze class or interface
        /// </summary>
        public static void AnalyzeTree<T>(IList<Triple> triples, SyntaxTree st, SemanticModel sem, FolderNode rootFolder)
            where T : TypeDeclarationSyntax
        {
            var root = st.GetRoot();
            var filePath = root.SyntaxTree.FilePath;
            var index = filePath.IndexOf(rootFolder.Name);
            filePath = index < 0 ? filePath : filePath[index..];
            var fileName = GetName(filePath);
            var fileNode = new FileNode(filePath, fileName);
            GetFolderChain(filePath, fileNode).ForEach(triples.Add);
            var declarations = root.DescendantNodes().OfType<T>();
            foreach (var declaration in declarations)
            {
                var node = sem.GetDeclaredSymbol(declaration).CreateTypeNode(declaration);
                if (node != null)
                {
                    triples.Add(new TripleDeclaredAt(node, fileNode));
                    GetInherits(triples, declaration, sem, node);
                    GetMethodsAll(triples, declaration, sem, node);
                }
            }
        }

        /// <summary>
        /// Member (field, property) initialization
        /// </summary>
        //public static void GetConstructsWithinClass(IList<Triple> triples, ClassDeclarationSyntax declaration, SemanticModel sem, ClassNode classNode)
        //{
        //    var creates = declaration.DescendantNodes().OfType<ObjectCreationExpressionSyntax>();
        //    foreach (var creation in creates)
        //    {
        //        var node = sem.GetTypeInfo(creation).CreateClassNode();
        //        triples.Add(new TripleConstruct(classNode, node));
        //    }
        //}

        /// <summary>
        /// Type inherited from BaseType
        /// </summary>
        public static void GetInherits(IList<Triple> triples, TypeDeclarationSyntax declaration, SemanticModel sem, TypeNode node)
        {
            if (declaration.BaseList == null) return;

            foreach (var baseTypeSyntax in declaration.BaseList.Types)
            {
                var parentNode = sem.GetTypeInfo(baseTypeSyntax.Type).CreateTypeNode();
                switch (node)
                {
                    case ClassNode classNode:
                        triples.Add(new TripleOfType(classNode, parentNode));
                        break;
                    case InterfaceNode interfaceNode when parentNode is InterfaceNode parentInterfaceNode:
                        triples.Add(new TripleOfType(interfaceNode, parentInterfaceNode));
                        break;
                }
            }
        }

        /// <summary>
        /// Class or Interface have some method AND some method can call another method AND some method can creates an object of class
        /// </summary>
        public static void GetMethodsAll(IList<Triple> triples, TypeDeclarationSyntax declaration, SemanticModel sem, TypeNode node)
        {
            var methods = declaration.DescendantNodes().OfType<MethodDeclarationSyntax>();
            foreach (var method in methods)
            {
                var methodNode = sem.GetDeclaredSymbol(method).CreateMethodNode(method);
                triples.Add(new TripleHave(node, methodNode));

                foreach (var syntax in method.DescendantNodes().OfType<ExpressionSyntax>())
                {
                    switch (syntax)
                    {
                        case ObjectCreationExpressionSyntax creation:
                            var classNode = sem.GetTypeInfo(creation).CreateClassNode();
                            triples.Add(new TripleConstruct(methodNode, classNode));
                            break;

                        case InvocationExpressionSyntax invocation:
                            if (sem.GetSymbolInfo(invocation).Symbol is IMethodSymbol invokedSymbol)
                            {
                                var invokedMethod = invokedSymbol.CreateMethodNode();
                                triples.Add(new TripleInvoke(methodNode, invokedMethod));
                            }
                            break;
                    }
                }
            }
        }
    }
}