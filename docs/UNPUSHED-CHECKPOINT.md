# Latest unpushed development checkpoint

The GitHub source at 928aecc includes the first implementation checkpoint, with 94 local functional checks and 13 browser checks. GitHub Actions additionally passed 5 native PostgreSQL checks and the package audit. Its Windows job stopped at a TLS test-certificate issue; the next commit corrects that fixture without weakening production certificate validation.

Further local development passed 116 functional and 17 browser checks, but the execution workspace stopped responding before those files could be uploaded. Those later counts do not describe the source currently in GitHub. Preserve this distinction when reporting readiness.

The unpushed work includes image crop/resize/brightness/grayscale and signed/CID assets; binary EML attachments and inline-image import; direct-MX routing with a separate disabled-by-default switch; global/per-sender concurrency controls; signed engagement signals and UTM attribution; categorized nine-template library; grouped top navigation; complete Help; external-editor adapter and trusted Windows shortcuts; DNS widget; foreign-key integrity and verified interrupted-upgrade recovery; container/Windows smoke scripts and an unsigned installer definition.

Important source files in that checkpoint: ImageAssets.cs, PngValidator.cs, MessageImport.cs, DirectDelivery.cs, EngagementTracking.cs, ExternalDesigner.cs, WorkspaceWidgets.cs, ContactSorting.cs, SubscriptionWorkers.cs, wwwroot/images.js, installer/CampaignStudio.iss, and associated changes/migrations/tests. Local migrations SenderCapacityAndSignals and DatabaseIntegrity were generated for SQLite and PostgreSQL. Latest local tests included concurrent send caps, no premature campaign completion, image checksums, signed asset/tracking URLs, preserved binary EML, and restored partial migrations.

Do not claim that this unpushed work is available in a downloadable build. The execution workspace must be recovered or the changes reimplemented before shipping them. Real-provider, external-designer, clean-PC, publisher-signing, and hosted-deployment checks remain separate release gates.
