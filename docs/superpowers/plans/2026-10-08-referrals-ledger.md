# Referral implementation ledger

Base: 377a18d9111a4c739f5c06a6dbc7549cf4f40a8b. Branch codex/videograbber-referrals-20261008. Owner approved implementation after selecting internal bonus balance. Installed relevant skills available; no installation needed.

Ruling: use 10%/10% recommended terms from the reviewed report вЂ” owner requested implementing that proposal, rather than selecting the alternative 20% variant.
Ruling: production activation remains disabled by default вЂ” money policies and new migration must be delivered as a concrete tested change before production authorization.
Ruling: implementation can split storage and UI against a fixed contract вЂ” subagent-driven-development explicitly delegates independent plan tasks; root owns payment/provider glue.

State: plan/contracts and first policy RED are next. No production DB/configuration or real payment/message changes.

Task1: policy RED CS0103 observed then10pure testsGREEN. Task2: Store11+policy10+bot8 initial29GREEN, tinyrewardRED observed/fixed; firstrecurringrewardRED observed/fixed. Fresh storagereview I1reserveddebtfundloss andI2competingfirstinvoice +leastprivileges reproducedRED5/5 thenfixed; latest20Storetests require integratedGREEN.
Task3: initialAPI/payment tests4/4GREEN verify1500в†’1350Rubinvoice,270?Starsinvoice240with20coupon,foreignquotebinding,partial/fullrefund/replay anddisabledsettlement. Test improved toactually repricecatalog6000afterinvoicecreation; newverifiedYoo refundlookup/webhook andrealTGlink regressions addednext. Sharedmoney locks20260918 coordinatedwithlink/adminmerge; sourcefinancialhistoryreconciliationguard; TGguest nowgetsmerged_into pointer preservinginvitation. No runtime/userDBchanges.
Task4: first49JS+22SeleniumsyntheticcasesGREEN. Freshreview reproduced staleMinibalance/accountrefresh andold503quotecallback state mutation; UIagentfixes+freshQA pending. NativeQA compiled0errors; test-only syntheticbonuscard fixture, 30width/pagechecks and6bonuscardcapturesnext.
Ruling: initial recurring subscription mayrewardfirstpaidreferrer; onlylater renewals donot. One-timepricingbenefits remainunavailableforrecurring.
Ruling: fullrefund preservesreturnedbonusamount viafresh365-day restorationlot; no externalcashvalue; explicitdocumentedrule. Rawpendingpricecheckoutcan'tcompete withfirst-only benefitreservation.
Ruling: no newskillsinstall necessary; allrequiredskills available. Existingpublicmain/sitepreview61 unchangedthrough implementation, no prodDDL/config/messages/payments.

## Final acceptance — 2026-10-08

Tasks1-4 implemented. Fresh storage/payment/interface reviews accepted after RED→GREEN repairs. Real Google/email unpaid accounts remain guest but qualify through authoritative primary-account eligibility; no test role workaround. Full Platform431 + localdesktop4 green; final affectedpayment/identity120 green, Core32, Infrastructure712+1unrelatedWhisper skip, Worker31, frontend52 and freshEdge25. See ACCEPTANCE.md for source chronology and exact artifacts. Final Managed and isolated QA compilations passed; normalDLL probe absent. Final QA process launch rejected by automatic policy, so previous30screens are distinct from finalgeneration source verification.

Final merge rulings: source promotion financialhistory requiresreconciliation; targetreservedpromotioninvoice blocksmerge untilverifiedcancel. Actualrepeatablecoupon3state andlegacytargetcheckoutregressionspass. Storedinvoice retry uses frozen terms when SKU removed; valid Yoo/Stars HTTP400 RED then120regressionsGREEN. Added newJSentryqueryversion beforefresh25browsercases to avoid oldservedcache.

Production disabled/unmodified. No new skill/dependency installations, real messages/payments/videos, commit/push/deploy. Candidate VERSION preview62; owner activation runbook and artifacts are concrete reviewable results.
