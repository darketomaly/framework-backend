namespace framework_backend;

public static class EmojiId
{
    public const string PlasticSubtractiveMerge = "<:plastic_subtractive_merge:1521878716102479922>";
    public const string PlasticMergeFrom = "<:plastic_merge_from:1521877966945259722>";
    public const string PlasticLabel = "<:plastic_label:1521877265779265617>";
    public const string PlasticNewBranch = "<:plastic_new_branch:1521877222456164522>";
    public const string PlasticCherryPick = "<:plastic_cherry_pick:1521877174603219044>";
    public const string PlasticCheckin = "<:plastic_checkin:1521865803602067529>";

    public const string GitPush = PlasticCheckin;
    public const string GitCommit = PlasticCheckin;
    public const string GitDeploy = SprintCompleted; // To do, replace for spaceship
    
    public const string TaskRevisit = "<:task_revisit:1521878670166327426>";
    public const string TaskCreated = "<:task_created:1521877022945841242>";
    public const string TaskReadyForReview = "<:task_ready_for_review:1521876982655221790>";
    public const string TaskRejected = "<:task_rejected:1521876911318634536>";
    public const string TaskApproved = "<:task_approved:1521876764002091078>";
    
    public const string SprintCompleted = "<:sprint_completed:1521876730195873983>";
    public const string SprintStart = "<:sprint_start:1521876688458481816>";
    
    public const string ReactionThumbsUp = "<:thumbs_up:1521877716453032047>";
    public const string ReactionThumbsDown = "<:thumbs_down:1521877749856337940>";
    public const string ReactionLaugh = "<:laugh:1526001338373112000>"; 
}