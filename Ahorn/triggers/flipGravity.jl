module FlipGravityTrigger
    using ..Ahorn, Maple
    @mapdef Trigger "vvvvvv/flipGravityTrigger" FlipGravity(x::Integer, y::Integer, width::Integer=8, height::Integer=8)

    const placements = Ahorn.PlacementDict(
        "Flip Gravity" => Ahorn.EntityPlacement(
            FlipGravity,
            "rectangle"
        ),
    )
end
