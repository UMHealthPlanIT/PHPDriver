using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utilities
{
    public class PasswordGenerator
    {
        /// <summary>
        /// Generates a new password with length and content based on parameters then sets Password property value with generated password
        /// </summary>
        /// <param name="useWord">Generate a password based on a dictionary word with a capitalized first letter</param>
        /// <param name="minCharLength">Minimum character lenth for character portion of the password</param>
        /// <param name="maxCharLength">Maximum character lenth for character portion of the password</param>
        /// <param name="numberDigitCount">Number of digits for the numbert portion of the password</param>
        public PasswordGenerator(bool useWord = true, int minCharLength = 8, int maxCharLength = 12, int numberDigitCount = 4)
        {
            string newPass = "";

            if (useWord)
            {
                //The array of words only contains words between 8 and 12 characters in length
                newPass += GenerateWord();
            }
            else
            {
                newPass += GenerateCharacters(minCharLength, maxCharLength);
            }

            newPass += GenerateIntAsString(numberDigitCount);

            Password = newPass;
        }

        public string Password { get; set; }

        private string GenerateWord()
        {
            Random ranNumber = new Random();
            int ind = ranNumber.Next(1, 1000);
            string word = GetWord(ind);

            string finalWord = word[0].ToString().ToUpper() + word.Substring(1);

            return finalWord;
        }

        private string GenerateIntAsString(int numberDigitCount)
        {
            string randNumber = "";
            Random rand = new Random();
            for (int i = 0; i < numberDigitCount; i++)
            {
                randNumber += rand.Next(0, 10).ToString();
            }

            return randNumber;
        }

        private string GenerateCharacters(int lowCharLength, int highCharLength)
        {
            string charString = "";
            string chars = "ABCDEFGHIJKLMNPQRSTUVWXYZabcdefghijklmnpqrstuvwxyz!@#$%^&*()-=_+[]{};:<>?";
            Random rand = new Random();
            int length = rand.Next(lowCharLength, highCharLength);
            for (int i = 0; i < length; i++)
            {
                charString += chars[rand.Next(0, chars.Length)];
            }

            return charString;
        }

        private string GetWord(int ind)
        {
            string[] englishWords = {
                "absolute", "abstract", "academic", "accepted", "accident", "according", "accurate", "achieved", "activated", "actively",
                "activity", "actually", "addiction", "additives", "adequate", "adjacent", "adjustment", "admission", "adventure", "advertise",
                "advocates", "affected", "afternoon", "agreement", "algorithm", "alligator", "alternative", "amendment", "amplifier", "analysis",
                "ancestor", "announced", "annoyance", "appliance", "applicants", "applicable", "applicants", "appreciate", "aquarium", "arguments",
                "artificial", "assignment", "assistance", "associate", "assurance", "atmosphere", "attractive", "automatic", "available", "awakening",
                "background", "basketball", "beautiful", "beginning", "believing", "benchmark", "beneficial", "biography", "biological", "biscuits",
                "blackboard", "blossoming", "boundary", "breathing", "brightness", "broadcast", "butterfly", "calculator", "calendar", "campaign",
                "candidate", "capability", "captured", "carefully", "caterpillar", "celebrity", "centered", "certainty", "challenge", "champion",
                "character", "charming", "chemical", "children", "circuits", "civilian", "classroom", "cleaning", "clearing", "closeness",
                "collective", "combining", "comfortable", "commander", "commenting", "commission", "committed", "commonly", "community", "companion",
                "comparing", "competing", "complaint", "completed", "complying", "composed", "concealed", "concerned", "conclusion", "condition",
                "confident", "conforming", "confusion", "congress", "connecting", "consider", "constant", "consumption", "contacted", "container",
                "contented", "continue", "contract", "contrary", "contribute", "controlling", "convenient", "converting", "conveying", "conviction",
                "corporate", "corrected", "counselor", "creating", "creation", "creative", "criterion", "criticism", "crocodile", "curiosity",
                "customary", "dangerous", "darkness", "databases", "daughter", "deadline", "dealing", "debating", "decision", "dedicated",
                "defensive", "definitely", "delicate", "delivery", "demanded", "demonstrate", "departure", "dependent", "descending", "deserving",
                "designing", "desperate", "destroyed", "detective", "detection", "determined", "developed", "diagnosis", "different", "difficult",
                "dimension", "direction", "discovery", "discussed", "disguised", "distract", "disturbed", "diversity", "divorce", "document",
                "domestic", "dominate", "doubtful", "downtown", "dramatic", "drastically", "earnings", "earthquake", "education", "effective",
                "efficient", "eighteen", "election", "electric", "emergency", "employed", "encounter", "encourage", "endeavor", "endurance",
                "enormous", "entertain", "entirely", "entitled", "envelope", "essential", "establish", "evaluate", "eventual", "everybody",
                "evidence", "evolution", "exactly", "excellent", "exciting", "executive", "exhausted", "existence", "expanded", "expected",
                "expensive", "experiment", "explained", "explorer", "exposure", "express", "extended", "exterior", "external", "extraordinary",
                "facility", "familiar", "fantastic", "farthest", "fashioned", "fastened", "feedback", "festival", "fighting", "financial",
                "fireworks", "flexible", "focusing", "football", "formation", "fortunate", "forward", "foundation", "frequent", "friendly",
                "frontier", "fulfilled", "function", "fundamental", "furthermore", "gallery", "gathered", "general", "generally", "generous",
                "gentleman", "genuine", "geography", "girlfriend", "glorious", "governing", "graduated", "gratitude", "grounded", "guaranteed",
                "guidance", "harmony", "headache", "healthier", "heavenly", "heighten", "helicopter", "heritage", "hilarious", "homeless",
                "homework", "horizon", "hospital", "humanity", "hundreds", "hurricane", "hypothesis", "identity", "ignition", "ignorance",
                "illustrate", "imaginary", "immediately", "immigrant", "imperial", "implement", "important", "impression", "improve", "incentive",
                "incident", "inclusive", "incredible", "individual", "industry", "informal", "inherent", "initiate", "innovate", "innovation",
                "inspired", "instance", "integrate", "integrity", "intensity", "interact", "interest", "interfere", "internal", "interview",
                "introduction", "intriguing", "invasion", "investigate", "invisible", "involved", "irregular", "isolated", "isolating", "jackpot",
                "janitor", "journey", "judgment", "junction", "junior", "justice", "keyboard", "kickoff", "kingdom", "knowledge", "laboratory",
                "landscape", "language", "laughter", "launching", "learning", "lecture", "legitimate", "lifestyle", "lighting", "limitless",
                "listening", "literature", "livelihood", "locality", "location", "logistic", "loneliness", "longitude", "luxurious", "machinery",
                "magazine", "magnetic", "majestic", "majority", "management", "mandatory", "manipulate", "manufacturer", "marketing", "masterpiece",
                "material", "maturity", "measure", "medication", "medicine", "memories", "mention", "merchant", "metabolic", "methodical",
                "military", "millionaire", "minimize", "ministry", "miracle", "mission", "mistaken", "mixture", "mobilize", "modification",
                "momentum", "monitoring", "monopoly", "morning", "mountain", "movement", "mysterious", "national", "navigate", "negotiate",
                "network", "nevertheless", "nighttime", "nonsense", "normalcy", "nostalgic", "notorious", "novelist", "numerous", "nutrition",
                "objective", "obstacle", "obtained", "occasion", "occupied", "offensive", "official", "omission", "operation", "opponent",
                "opportunity", "optimism", "orchestra", "ordinary", "organic", "orientation", "original", "outdoor", "outgoing", "outlined",
                "outlook", "overcome", "overload", "ownership", "packaging", "painting", "paradise", "parallel", "paralyzed", "parameter",
                "parental", "particle", "passenger", "passport", "patience", "payment", "peacemaker", "pedestrian", "penalty", "percentage",
                "perception", "perfection", "permanent", "permission", "permissible", "personal", "perspective", "persuade", "petition",
                "phantom", "philosophy", "physical", "pioneer", "pipeline", "persistence", "placement", "platform", "pleasure", "polarized",
                "political", "pollution", "popularity", "portfolio", "position", "positive", "possessed", "possible", "practical", "practitioner",
                "precaution", "preceding", "precious", "precisely", "predict", "preference", "premises", "preparation", "president", "pressure",
                "prestige", "previous", "princess", "priority", "privilege", "probability", "procedure", "proceeding", "proclaim", "productive",
                "profession", "professor", "profile", "profound", "progress", "project", "prolonged", "prominent", "promising", "promptly",
                "propaganda", "properly", "property", "proposal", "prosperity", "protect", "protest", "provoke", "psychology", "publicity",
                "purchase", "purpose", "qualified", "quantity", "question", "quickly", "radiation", "radiation", "radical", "railroad",
                "rallying", "randomly", "rapidly", "reaction", "rebellion", "recalling", "receipt", "receiving", "recognition", "recording",
                "recovering", "recreation", "recruiting", "rectangle", "recycling", "reducing", "redundant", "reference", "refrigerator", "register",
                "regular", "rejected", "relating", "relation", "relative", "relaxation", "relieved", "relocation", "remaining", "remarkable",
                "remember", "reminders", "removal", "renowned", "repaired", "repeated", "replaced", "reporting", "represent", "reproduce",
                "requested", "research", "residence", "resistant", "resolution", "respective", "responded", "response", "restoration", "restricted",
                "resulting", "retaining", "retention", "retrieval", "retrofit", "reunion", "revenge", "reversible", "revision", "revolution",
                "rewarding", "richness", "righteous", "rigorous", "roadway", "romantic", "rotation", "satisfied", "satisfying", "scenario",
                "schedule", "scholar", "scientific", "scramble", "sculpture", "searching", "seasonal", "secondary", "secretary", "segment",
                "selected", "separate", "sequence", "serenity", "session", "shelter", "shortly", "showcase", "shutting", "sightseeing",
                "significance", "silence", "similar", "simplified", "simulator", "situation", "slightly", "smallest", "smoothly", "socially",
                "software", "solution", "somehow", "somewhat", "sovereign", "specific", "spectacle", "spokesman", "sponsor", "spotlight",
                "stability", "starting", "statement", "statistical", "staying", "stimulus", "stirring", "strategic", "strongly", "structured",
                "struggle", "stubborn", "studying", "substance", "substitute", "suburb", "successful", "suddenly", "suffering", "sufficient",
                "suggest", "suitable", "superior", "supervisor", "supplement", "support", "supposed", "surprising", "survival", "suspense",
                "sustain", "swimming", "syndrome", "synthetic", "systems", "technical", "technique", "technological", "technology", "teenager",
                "telegraph", "telescope", "temperature", "temporary", "tenacity", "tendency", "terminal", "territory", "testimonial", "testing",
                "textbook", "theater", "therefore", "thorough", "thousand", "threaten", "throughout", "throwing", "tightrope", "tolerance",
                "tomorrow", "topical", "tormenting", "tortoise", "touchdown", "towards", "traditional", "transition", "translate", "transport",
                "treatment", "triangle", "ultimate", "understand", "universal", "universe", "university", "vegetable", "vegetarian", "ventilation",
                "vocabulary", "volunteer", "warehouse", "wholesaler", "wonderful", "abandoned", "absolutely", "abundance", "accessing", "accordingly",
                "accounting", "activated", "addictive", "additional", "adventure", "advertising", "advisable", "aesthetic", "aggressive", "allergies",
                "allocation", "alternative", "amendment", "anniversary", "announcement", "antibiotics", "anticipated", "application", "appointing",
                "appointment", "appreciated", "appropriate", "architect", "arrangement", "attendance", "attractive", "background", "beautifully",
                "beginnings", "benefiting", "bewildered", "boundaries", "brainstorm", "breakthrough", "broadcasting", "bureaucracy", "capability",
                "celebrating", "celebration", "certificates", "challenging", "characterize", "charismatic", "circumstance", "combination",
                "communication", "communities", "compensation", "competitors", "complaining", "complement", "comprehensive", "concentration",
                "conceptual", "conclusion", "confidence", "confinement", "confirmation", "conformity", "connectivity", "conservation", "considerate",
                "consistency", "constantly", "consultant", "consuming", "contaminated", "contemporary", "continuation", "contributing", "conversion",
                "convincing", "cooperation", "cooperative", "coordinate", "corporate", "correction", "correspond", "counseling", "counterfeit",
                "creativity", "credibility", "cultivating", "cultivation", "cumulative", "declaration", "decorating", "decreasing", "definitely",
                "deliberate", "demonstrate", "depression", "designation", "desperation", "destination", "determination", "development", "disappointed",
                "discovered", "discussing", "disruption", "distraction", "distribution", "diversified", "dramatically", "effectively", "efficiency",
                "eliminating", "employment", "encountered", "encouraging", "engineering", "entertaining", "enthusiastic", "environment", "equivalent",
                "evaluation", "evolution", "exaggerate", "examination", "exceeding", "excellent", "exceptional", "exchanging", "exclusively",
                "exercising", "exhibition", "experience", "experiment", "explaining", "exploration", "expressive", "extensively", "extraordinary",
                "facilitating", "fascinating", "federation", "forecasting", "fundamental", "geological", "government", "graduation", "guaranteed",
                "handwriting", "happiness", "harmonious", "headquarters", "highlighted", "hospitality", "illustration", "imaginative", "immigration",
                "improvement", "incorporated", "industrial", "inevitable", "influential", "information", "infrastructure", "inhabitant", "initiating",
                "innovation", "inspection", "inspiration", "installation", "instrument", "integration", "intelligent", "intervention", "introduction",
                "investigate", "investment", "invisible", "invitation", "journalism", "landscaping", "legislature", "limitations", "liquidation",
                "maintenance", "manipulation", "manufacturing", "marketplace", "masterpiece", "meditation", "memorandum", "methodology", "military",
                "minimization", "modification", "monitoring", "motivation", "negotiation", "neighboring", "notification", "objectives", "observation",
                "occupation", "opportunity", "organization", "orientation", "originating", "outstanding", "participation", "partnership", "passionate",
                "performance", "perseverance", "perspective", "photography", "physical", "plagiarism", "population", "possession", "preparation",
                "presentation", "preservation", "procedures", "profession", "proficiency", "promotion", "properties", "prospective", "protection",
                "psychological", "publication", "quantitative", "rearranging", "realization", "reasonable", "recognition", "recommendation", "recruitment",
                "redundancy", "reflection", "relevance", "reliability", "relocation", "remembrance", "renovation", "reputation", "reservation", "resolution",
                "respective", "restaurant", "restriction", "retirement", "revolution", "satisfaction", "significant", "solicitation", "specialization",
                "sponsorship", "stabilization", "strategically", "subsequent", "subsidiary", "substantial", "successfully", "supplement", "sustainability",
                "sympathetic", "technology", "testimonial", "transaction", "transformation", "transition", "transportation", "understanding", "unfortunately",
                "university", "unpredictable", "unquestionably", "utilization", "vulnerability", "weathering", "wilderness", "willingness", "wrestling",
                "abandonment", "absolutely", "absorption", "acceleration", "accelerator", "accessible", "accompanying", "accomplished", "accountable",
                "accumulated", "achievement", "acknowledged", "acquisition", "adaptation", "additional", "addressing", "adequately", "adjustable",
                "adjustment", "admiration", "admissible", "advancement", "advantages", "adventure", "aerodynamic", "affiliation", "afterward",
                "aggressively", "alternatively", "anticipated", "appreciated", "appropriate", "architecture", "articulation", "artificial",
                "ascendancy", "assessment", "assignment", "assistance", "associated", "assumptions", "atmospheric", "attachment", "attraction",
                "attractive", "authenticity", "availability", "bankruptcy", "beautifully", "benevolent", "broadcasting", "calamities", "capability",
                "cancellation", "cancellation", "candidate", "celebrating", "celebration", "championship", "characteristic", "charismatic",
                "communication", "comparative", "compensated", "competition", "complementary", "complicated", "concentration", "conditional",
                "congratulate", "consequence", "consistency", "consequence", "considerable", "construction", "contamination", "contemporary",
                "conveniently", "conventional", "conversation", "cooperative", "coordination", "corporation", "correspondence", "corresponding",
                "credibility", "customarily", "declaration", "decoration", "definitely", "deliberate", "deliverable", "demonstrate", "department",
                "depression", "designation", "destination", "determination", "development", "difference", "discovering", "discussion", "dispatched",
                "distinction", "distributor", "dramatically", "effectively", "efficiently", "electronic", "elimination", "employment", "encouraging",
                "engineering", "enthusiasm", "environment", "establishment", "evaluation", "exaggerate", "examination", "exceptional", "exchanging",
                "exhibition", "experience", "experiment", "exploration", "expression", "extensively", "facilitation", "fascination", "federation",
                "forecasting", "fulfillment", "geological", "government", "graduation", "guaranteed", "handwriting", "happiness", "harmonious",
                "headquarters", "highlighted", "hospitality", "illustration", "imaginative", "implementation", "incorporated", "industrial",
                "inevitably", "influential", "information", "infrastructure", "inhabitant", "initiating", "innovation", "inspection", "inspiration",
                "installation", "instrument", "integration", "intelligent", "intention", "interaction", "interference", "intervention", "introduction",
                "investment", "invitation", "journalism", "justification", "landscape", "legislation", "limitations", "liquidation", "maintenance",
                "manipulation", "manufacture", "marketplace", "masterpiece", "meditation", "memorandum", "methodology", "motivation", "negotiation",
                "notification", "objectives", "observation", "opportunity", "organization", "orientation", "originating", "outstanding", "participation",
                "partnership", "performance", "perseverance", "perspective", "photography", "plagiarism", "population", "possession", "preparation",
                "presentation", "preservation", "procedures", "profession", "proficiency", "promotion", "properties", "prospective", "protection",
                "psychological", "publication", "quantitative", "rearranging", "realization", "reasonable", "recognition", "recommendation", "recruitment",
                "redundancy", "reflection", "relevance", "reliability", "relocation", "remembrance", "renovation", "reputation", "reservation", "resolution",
                "respective", "restaurant", "restriction", "retirement", "revolution", "satisfaction", "significant", "solicitation", "specialization",
                "sponsorship", "stabilization", "strategically", "subsequent", "subsidiary", "substantial", "successfully", "supplement", "sustainability",
                "sympathetic", "technology", "testimonial", "transaction", "transformation", "transition", "transportation", "understanding", "unfortunately",
                "university", "unpredictable", "unquestionably", "utilization", "vulnerability", "weathering", "wilderness", "willingness", "wrestling"
            };

            return englishWords[ind];
        }
    }
}
