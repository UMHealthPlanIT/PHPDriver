using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Driver.ExternalResources
{
    class Shared
    {
        #region Shared
        /// <remarks/>
        [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.33440")]
        [System.SerializableAttribute()]
        [System.Diagnostics.DebuggerStepThroughAttribute()]
        [System.ComponentModel.DesignerCategoryAttribute("code")]
        [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://vo.edge.fm.cms.hhs.gov")]
        [System.Xml.Serialization.XmlRootAttribute("commonOutboundFileHeader", Namespace = "http://vo.edge.fm.cms.hhs.gov", IsNullable = false)]
        public partial class CommonOutboundFileHeader
        {

            private string outboundFileIdentifierField;

            private string outboundFileGenerationDateTimeField;

            private string inboundFileIdentifierField;

            private string interfaceControlReleaseNumberField;

            private string edgeServerVersionField;

            private string edgeServerProcessIdentifierField;

            private string inboundFileGenerationDateTimeField;

            private string outboundFileTypeCodeField;

            private string edgeServerIdentifierField;

            private string issuerIDField;

            private string inboundFileSubmissionDateTimeField;

            private string inboundFileSubmissionTypeField;

            /// <remarks/>
            public string outboundFileIdentifier
            {
                get
                {
                    return this.outboundFileIdentifierField;
                }
                set
                {
                    this.outboundFileIdentifierField = value;
                }
            }

            /// <remarks/>
            public string outboundFileGenerationDateTime
            {
                get
                {
                    return this.outboundFileGenerationDateTimeField;
                }
                set
                {
                    this.outboundFileGenerationDateTimeField = value;
                }
            }

            /// <remarks/>
            public string inboundFileIdentifier
            {
                get
                {
                    return this.inboundFileIdentifierField;
                }
                set
                {
                    this.inboundFileIdentifierField = value;
                }
            }

            /// <remarks/>
            public string interfaceControlReleaseNumber
            {
                get
                {
                    return this.interfaceControlReleaseNumberField;
                }
                set
                {
                    this.interfaceControlReleaseNumberField = value;
                }
            }

            /// <remarks/>
            public string edgeServerVersion
            {
                get
                {
                    return this.edgeServerVersionField;
                }
                set
                {
                    this.edgeServerVersionField = value;
                }
            }

            /// <remarks/>
            public string edgeServerProcessIdentifier
            {
                get
                {
                    return this.edgeServerProcessIdentifierField;
                }
                set
                {
                    this.edgeServerProcessIdentifierField = value;
                }
            }

            /// <remarks/>
            public string inboundFileGenerationDateTime
            {
                get
                {
                    return this.inboundFileGenerationDateTimeField;
                }
                set
                {
                    this.inboundFileGenerationDateTimeField = value;
                }
            }

            /// <remarks/>
            public string outboundFileTypeCode
            {
                get
                {
                    return this.outboundFileTypeCodeField;
                }
                set
                {
                    this.outboundFileTypeCodeField = value;
                }
            }

            /// <remarks/>
            public string edgeServerIdentifier
            {
                get
                {
                    return this.edgeServerIdentifierField;
                }
                set
                {
                    this.edgeServerIdentifierField = value;
                }
            }

            /// <remarks/>
            public string issuerID
            {
                get
                {
                    return this.issuerIDField;
                }
                set
                {
                    this.issuerIDField = value;
                }
            }

            /// <remarks/>
            public string inboundFileSubmissionDateTime
            {
                get
                {
                    return this.inboundFileSubmissionDateTimeField;
                }
                set
                {
                    this.inboundFileSubmissionDateTimeField = value;
                }
            }

            /// <remarks/>
            public string inboundFileSubmissionType
            {
                get
                {
                    return this.inboundFileSubmissionTypeField;
                }
                set
                {
                    this.inboundFileSubmissionTypeField = value;
                }
            }
        }

        /// <remarks/>
        [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.33440")]
        [System.SerializableAttribute()]
        [System.Diagnostics.DebuggerStepThroughAttribute()]
        [System.ComponentModel.DesignerCategoryAttribute("code")]
        [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://vo.edge.fm.cms.hhs.gov")]
        [System.Xml.Serialization.XmlRootAttribute("submissionProcessingStatusType", Namespace = "http://vo.edge.fm.cms.hhs.gov", IsNullable = false)]
        public partial class SubmissionProcessingStatusType
        {

            private string statusTypeCodeField;

            /// <remarks/>
            public string statusTypeCode
            {
                get
                {
                    return this.statusTypeCodeField;
                }
                set
                {
                    this.statusTypeCodeField = value;
                }
            }
        }

        /// <remarks/>
        [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.33440")]
        [System.SerializableAttribute()]
        [System.Diagnostics.DebuggerStepThroughAttribute()]
        [System.ComponentModel.DesignerCategoryAttribute("code")]
        [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://vo.edge.fm.cms.hhs.gov")]
        [System.Xml.Serialization.XmlRootAttribute("claimCountMessageType", Namespace = "http://vo.edge.fm.cms.hhs.gov", IsNullable = false)]
        public partial class ClaimCountMessageType
        {

            private string recordsReceivedField;

            private string recordsAcceptedField;

            private string recordsResolvedField;

            private string recordsRejectedField;

            private string newRecordsAcceptedField;

            /// <remarks/>
            [System.Xml.Serialization.XmlElementAttribute(DataType = "integer")]
            public string recordsReceived
            {
                get
                {
                    return this.recordsReceivedField;
                }
                set
                {
                    this.recordsReceivedField = value;
                }
            }

            /// <remarks/>
            [System.Xml.Serialization.XmlElementAttribute(DataType = "integer")]
            public string recordsAccepted
            {
                get
                {
                    return this.recordsAcceptedField;
                }
                set
                {
                    this.recordsAcceptedField = value;
                }
            }

            /// <remarks/>
            [System.Xml.Serialization.XmlElementAttribute(DataType = "integer")]
            public string recordsResolved
            {
                get
                {
                    return this.recordsResolvedField;
                }
                set
                {
                    this.recordsResolvedField = value;
                }
            }

            /// <remarks/>
            [System.Xml.Serialization.XmlElementAttribute(DataType = "integer")]
            public string recordsRejected
            {
                get
                {
                    return this.recordsRejectedField;
                }
                set
                {
                    this.recordsRejectedField = value;
                }
            }

            /// <remarks/>
            [System.Xml.Serialization.XmlElementAttribute(DataType = "integer")]
            public string newRecordsAccepted
            {
                get
                {
                    return this.newRecordsAcceptedField;
                }
                set
                {
                    this.newRecordsAcceptedField = value;
                }
            }
        }

        /// <remarks/>
        [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.33440")]
        [System.SerializableAttribute()]
        [System.Diagnostics.DebuggerStepThroughAttribute()]
        [System.ComponentModel.DesignerCategoryAttribute("code")]
        [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://vo.edge.fm.cms.hhs.gov")]
        [System.Xml.Serialization.XmlRootAttribute("errorMessageType", Namespace = "http://vo.edge.fm.cms.hhs.gov", IsNullable = false)]
        public partial class ErrorMessageType
        {

            private string offendingElementNameField;

            private string offendingElementValueField;

            private string[] offendingElementErrorTypeCodeField;

            private string[] offendingElementErrorTypeMessageField;

            private string[] offendingElementErrorTypeDetailField;

            /// <remarks/>
            public string offendingElementName
            {
                get
                {
                    return this.offendingElementNameField;
                }
                set
                {
                    this.offendingElementNameField = value;
                }
            }

            /// <remarks/>
            public string offendingElementValue
            {
                get
                {
                    return this.offendingElementValueField;
                }
                set
                {
                    this.offendingElementValueField = value;
                }
            }

            /// <remarks/>
            [System.Xml.Serialization.XmlElementAttribute("offendingElementErrorTypeCode", IsNullable = true)]
            public string[] offendingElementErrorTypeCode
            {
                get
                {
                    return this.offendingElementErrorTypeCodeField;
                }
                set
                {
                    this.offendingElementErrorTypeCodeField = value;
                }
            }

            /// <remarks/>
            [System.Xml.Serialization.XmlElementAttribute("offendingElementErrorTypeMessage", IsNullable = true)]
            public string[] offendingElementErrorTypeMessage
            {
                get
                {
                    return this.offendingElementErrorTypeMessageField;
                }
                set
                {
                    this.offendingElementErrorTypeMessageField = value;
                }
            }

            /// <remarks/>
            [System.Xml.Serialization.XmlElementAttribute("offendingElementErrorTypeDetail", IsNullable = true)]
            public string[] offendingElementErrorTypeDetail
            {
                get
                {
                    return this.offendingElementErrorTypeDetailField;
                }
                set
                {
                    this.offendingElementErrorTypeDetailField = value;
                }
            }
        }

        /// <remarks/>
        [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.33440")]
        [System.SerializableAttribute()]
        [System.Diagnostics.DebuggerStepThroughAttribute()]
        [System.ComponentModel.DesignerCategoryAttribute("code")]
        [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://vo.edge.fm.cms.hhs.gov")]
        [System.Xml.Serialization.XmlRootAttribute("fileProcessingResultStatus", Namespace = "http://vo.edge.fm.cms.hhs.gov", IsNullable = false)]
        public partial class FileProcessingResultStatus
        {

            private CommonOutboundFileHeader includedFileHeaderField;

            private SubmissionProcessingStatusType classifyingProcessingStatusTypeField;

            private ErrorMessageType[] recordedErrorField;

            /// <remarks/>
            public CommonOutboundFileHeader includedFileHeader
            {
                get
                {
                    return this.includedFileHeaderField;
                }
                set
                {
                    this.includedFileHeaderField = value;
                }
            }

            /// <remarks/>
            public SubmissionProcessingStatusType classifyingProcessingStatusType
            {
                get
                {
                    return this.classifyingProcessingStatusTypeField;
                }
                set
                {
                    this.classifyingProcessingStatusTypeField = value;
                }
            }

            /// <remarks/>
            [System.Xml.Serialization.XmlElementAttribute("recordedError")]
            public ErrorMessageType[] recordedError
            {
                get
                {
                    return this.recordedErrorField;
                }
                set
                {
                    this.recordedErrorField = value;
                }
            }
        }
        #endregion
    }
}
