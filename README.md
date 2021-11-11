This application will parse the provided SQL and render it as requested.  The SQL content is expected to be piped application

It accepts the following command line arguments

| command line argument | description | default value |
| - | - | - |
| `-f`/`--format` | Controls the output format (select between `Json`, `Yaml`, `Html`) | `Json` |
| `-t`/`--to-file` | Write the output to a file, instead of StdOut, and the output filepath is written to StdOyt | Send output to StdOut |
| `-o`/`--output-path` | The file path to use when `--to-file` has been included | `out.{format}` |
| `-l`/`--log-destination` | Where to send log information (select between `None`, `StdOut`, `StdError`, `Output`) | `None` |

## Examples
1. `echo "PRINT 'text'" | dotnet run` yields
   ```
   {
     "TypeName": "TSqlScript",
     "Text": "PRINT 'text'\n",
     "Children": [
       {
         "TypeName": "TSqlBatch",
         "Text": "PRINT 'text'",
         "Children": [
           {
             "TypeName": "PrintStatement",
             "Text": "PRINT 'text'",
             "Children": [
               {
                 "TypeName": "StringLiteral",
                 "Text": "'text'",
                 "Properties": [
                   {
                     "Name": "IsLargeObject",
                     "Value": "False"
                   },
                   {
                     "Name": "IsNational",
                     "Value": "False"
                   },
                   {
                     "Name": "LiteralType",
                     "Value": "String"
                   },
                   {
                     "Name": "Value",
                     "Value": "text"
                   }
                 ]
               }
             ]
           }
         ]
       }
     ]
   }
   ```
1. `echo "SELECT * FROM tbl_Foo" | dotnet run --format Yaml` yields
   ```
   TypeName: TSqlScript
   Text: >
     SELECT * FROM tbl_Foo
   Children:
   - TypeName: TSqlBatch
     Text: SELECT * FROM tbl_Foo
     Children:
     - TypeName: SelectStatement
       Text: SELECT * FROM tbl_Foo
       Children:
       - TypeName: QuerySpecification
         Text: SELECT * FROM tbl_Foo
         Children:
         - TypeName: SelectStarExpression
           Text: '*'
         - TypeName: FromClause
           Text: FROM tbl_Foo
           Children:
           - TypeName: NamedTableReference
             Text: tbl_Foo
             Children:
             - TypeName: SchemaObjectName
               Text: tbl_Foo
               Children:
               - TypeName: Identifier
                 Text: tbl_Foo
                 Properties:
                 - Name: QuoteType
                   Value: NotQuoted
                 - Name: Value
                   Value: tbl_Foo
               Properties:
               - Name: Count
                 Value: 1
             Properties:
             - Name: ForPath
               Value: False
         Properties:
         - Name: UniqueRowFilter
           Value: NotSpecified
   ```
